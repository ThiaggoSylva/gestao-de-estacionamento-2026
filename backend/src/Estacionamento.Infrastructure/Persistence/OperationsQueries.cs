using Estacionamento.Application.Common;
using Estacionamento.Application.Operations;
using Estacionamento.Domain.Operations;
using Estacionamento.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Estacionamento.Infrastructure.Persistence;

public sealed class OperationsQueries(IdentityDbContext database, IDocumentProtector documents)
    : IOperationsQueries
{
    public async Task<IReadOnlyList<SpotDto>> GetSpotsAsync(CancellationToken cancellationToken)
    {
        var spots = await database.ParkingSpots
            .AsNoTracking()
            .OrderBy(spot => spot.Code)
            .ToListAsync(cancellationToken);

        var openOccupancies = await database.Occupancies
            .AsNoTracking()
            .Where(occupancy => occupancy.EndedAtUtc == null)
            .Select(occupancy => new
            {
                occupancy.ParkingSpotId,
                PublicId = occupancy.ParkingSession!.PublicId,
                occupancy.ParkingSession.Plate
            })
            .ToDictionaryAsync(item => item.ParkingSpotId, cancellationToken);

        return spots.Select(spot =>
        {
            openOccupancies.TryGetValue(spot.Id, out var open);
            return new SpotDto(
                spot.Id,
                spot.Code,
                spot.IsActive,
                open is not null,
                open?.PublicId,
                open?.Plate);
        }).ToList();
    }

    public async Task<TariffDto?> GetCurrentTariffAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var tariff = await database.Tariffs
            .AsNoTracking()
            .Where(item => item.EffectiveFromUtc <= now)
            .OrderByDescending(item => item.EffectiveFromUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return tariff is null ? null : new TariffDto(tariff.Id, tariff.DailyRate, tariff.EffectiveFromUtc);
    }

    public async Task<PagedResult<AttendanceSummaryDto>> SearchAttendancesAsync(
        string? plate,
        string? publicId,
        int page,
        int pageSize,
        bool includeFinancial,
        CancellationToken cancellationToken)
    {
        var query = database.ParkingSessions
            .AsNoTracking()
            .Include(session => session.ParkingSpot)
            .Include(session => session.WithdrawalRequests)
            .Include(session => session.Charges)
            .Include(session => session.Payments)
            .Include(session => session.Adjustments)
            .AsSplitQuery()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(publicId))
        {
            query = query.Where(session => session.PublicId == publicId);
        }

        if (!string.IsNullOrWhiteSpace(plate))
        {
            query = query.Where(session => session.PlateNormalized == plate);
        }

        var total = await query.CountAsync(cancellationToken);
        var sessions = await query
            .OrderByDescending(session => session.EntryAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = sessions.Select(session => MapSummary(session, includeFinancial)).ToList();
        return new PagedResult<AttendanceSummaryDto>(items, page, pageSize, total);
    }

    public async Task<AttendanceDetailDto?> GetAttendanceAsync(
        Guid id,
        bool includeFinancial,
        bool includeFullDocument,
        CancellationToken cancellationToken)
    {
        var session = await database.ParkingSessions
            .AsNoTracking()
            .Include(item => item.ParkingSpot)
            .Include(item => item.Inspections)
            .Include(item => item.WithdrawalRequests)
            .Include(item => item.Charges)
            .Include(item => item.Payments)
            .Include(item => item.Adjustments)
            .Include(item => item.Authorizations)
            .Include(item => item.Occupancies)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (session is null)
        {
            return null;
        }

        var withdrawal = session.WithdrawalRequests.SingleOrDefault();
        var charge = session.Charges.SingleOrDefault();
        var (charged, paid, outstanding) = BalanceCalculator.Calculate(session);
        var actorNames = await LoadActorNamesAsync(
            session.Payments.Select(payment => payment.CreatedByUserId)
                .Concat(session.Adjustments.Select(adjustment => adjustment.CreatedByUserId))
                .Concat(session.Authorizations.Select(authorization => authorization.CreatedByUserId)),
            cancellationToken);

        var document = includeFullDocument
            ? documents.Unprotect(session.HolderDocumentProtected)
            : documents.Mask(documents.Unprotect(session.HolderDocumentProtected));

        var hasDocumentAuthorization = session.Authorizations
            .Any(authorization => authorization.Type == ExceptionAuthorizationType.DocumentDivergence);
        var hasBalanceAuthorization = session.Authorizations
            .Any(authorization => authorization.Type == ExceptionAuthorizationType.OutstandingBalance);
        var documentApproved = withdrawal?.DocumentVerification == DocumentVerification.Approved;
        var canDeliver = includeFinancial
            && withdrawal is not null
            && charge is not null
            && session.State is ParkingSessionState.AwaitingWithdrawal or ParkingSessionState.Searching
            && session.Inspections.All(inspection => inspection.Type != InspectionType.Exit)
            && (documentApproved || hasDocumentAuthorization)
            && (outstanding <= 0 || hasBalanceAuthorization);

        return new AttendanceDetailDto(
            session.Id,
            session.PublicId,
            session.Plate,
            session.Model,
            session.HolderName,
            document,
            session.State.ToString(),
            session.ParkingSpotId,
            session.ParkingSpot?.Code ?? string.Empty,
            session.EntryAtUtc,
            session.DeliveredAtUtc,
            session.Inspections.FirstOrDefault(inspection => inspection.Type == InspectionType.Entry)?.Text ?? string.Empty,
            session.Inspections.FirstOrDefault(inspection => inspection.Type == InspectionType.Exit)?.Text,
            withdrawal is null
                ? null
                : new WithdrawalDetailDto(
                    withdrawal.Id,
                    withdrawal.State.ToString(),
                    withdrawal.RequestedAtUtc,
                    withdrawal.DocumentVerification.ToString(),
                    withdrawal.VerificationNotes,
                    withdrawal.SearchStartedAtUtc,
                    charge?.DailyCount ?? 0,
                    includeFinancial ? charge?.TotalAmount : null),
            includeFinancial ? new FinancialDto(charged, paid, outstanding) : null,
            includeFinancial
                ? session.Payments
                    .OrderBy(payment => payment.PaidAtUtc)
                    .Select(payment => new PaymentDto(
                        payment.Id,
                        payment.Amount,
                        payment.Method.ToString(),
                        payment.PaidAtUtc,
                        payment.CreatedByUserId,
                        actorNames.GetValueOrDefault(payment.CreatedByUserId)))
                    .ToList()
                : [],
            includeFinancial
                ? session.Adjustments
                    .OrderBy(adjustment => adjustment.CreatedAtUtc)
                    .Select(adjustment => new AdjustmentDto(
                        adjustment.Id,
                        adjustment.Amount,
                        adjustment.Reason,
                        adjustment.CreatedAtUtc,
                        adjustment.CreatedByUserId,
                        actorNames.GetValueOrDefault(adjustment.CreatedByUserId)))
                    .ToList()
                : [],
            includeFinancial
                ? session.Authorizations
                    .OrderBy(authorization => authorization.CreatedAtUtc)
                    .Select(authorization => new AuthorizationDto(
                        authorization.Id,
                        authorization.Type.ToString(),
                        authorization.Reason,
                        authorization.CreatedAtUtc,
                        authorization.CreatedByUserId,
                        actorNames.GetValueOrDefault(authorization.CreatedByUserId)))
                    .ToList()
                : [],
            includeFinancial && session.State == ParkingSessionState.Active && withdrawal is null,
            withdrawal?.State == WithdrawalState.Awaiting,
            canDeliver);
    }

    public async Task<IReadOnlyList<QueueItemDto>> GetQueueAsync(
        bool includeFinancial,
        CancellationToken cancellationToken)
    {
        var requests = await database.WithdrawalRequests
            .AsNoTracking()
            .Include(request => request.ParkingSession)!.ThenInclude(session => session!.ParkingSpot)
            .Include(request => request.ParkingSession)!.ThenInclude(session => session!.Charges)
            .Include(request => request.ParkingSession)!.ThenInclude(session => session!.Payments)
            .Include(request => request.ParkingSession)!.ThenInclude(session => session!.Adjustments)
            .AsSplitQuery()
            .OrderBy(request => request.State == WithdrawalState.Delivered)
            .ThenBy(request => request.RequestedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var actorNames = await LoadActorNamesAsync(
            requests.Select(request => request.RequestedByUserId)
                .Concat(requests.Where(request => request.SearchStartedByUserId.HasValue)
                    .Select(request => request.SearchStartedByUserId!.Value)),
            cancellationToken);

        return requests.Select(request =>
        {
            var session = request.ParkingSession
                ?? throw new InvalidOperationException("Pedido de retirada sem atendimento carregado.");
            var charge = session.Charges.SingleOrDefault();
            var (_, _, outstanding) = BalanceCalculator.Calculate(session);
            return new QueueItemDto(
                request.Id,
                session.Id,
                session.PublicId,
                session.Plate,
                session.Model,
                session.ParkingSpot?.Code ?? string.Empty,
                request.State.ToString(),
                request.RequestedAtUtc,
                request.SearchStartedAtUtc,
                request.DocumentVerification.ToString(),
                charge?.DailyCount ?? 0,
                includeFinancial ? charge?.TotalAmount : null,
                includeFinancial ? outstanding : null,
                actorNames.GetValueOrDefault(request.RequestedByUserId),
                request.SearchStartedByUserId.HasValue
                    ? actorNames.GetValueOrDefault(request.SearchStartedByUserId.Value)
                    : null,
                request.State == WithdrawalState.Awaiting);
        }).ToList();
    }

    public async Task<IReadOnlyList<AuditEntryDto>?> GetAuditAsync(
        Guid attendanceId,
        CancellationToken cancellationToken)
    {
        var exists = await database.ParkingSessions
            .AsNoTracking()
            .AnyAsync(session => session.Id == attendanceId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var entries = await database.AuditEntries
            .AsNoTracking()
            .Where(entry => entry.ParkingSessionId == attendanceId)
            .OrderBy(entry => entry.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        var actorNames = await LoadActorNamesAsync(
            entries.Select(entry => entry.ActorUserId),
            cancellationToken);

        return entries.Select(entry => new AuditEntryDto(
            entry.Id,
            entry.Action,
            entry.Details,
            entry.OccurredAtUtc,
            entry.ActorUserId,
            actorNames.GetValueOrDefault(entry.ActorUserId))).ToList();
    }

    private AttendanceSummaryDto MapSummary(ParkingSession session, bool includeFinancial)
    {
        var (charged, paid, outstanding) = BalanceCalculator.Calculate(session);
        return new AttendanceSummaryDto(
            session.Id,
            session.PublicId,
            session.Plate,
            session.Model,
            session.State.ToString(),
            session.ParkingSpot?.Code ?? string.Empty,
            session.EntryAtUtc,
            session.WithdrawalRequests.Select(request => (DateTime?)request.RequestedAtUtc).FirstOrDefault(),
            session.DeliveredAtUtc,
            documents.Mask(documents.Unprotect(session.HolderDocumentProtected)),
            includeFinancial ? charged : null,
            includeFinancial ? paid : null,
            includeFinancial ? outstanding : null);
    }

    private async Task<Dictionary<Guid, string?>> LoadActorNamesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await database.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.UserName, cancellationToken);
    }
}
