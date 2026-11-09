using Estacionamento.Application.Common;
using Estacionamento.Domain.Common;
using Estacionamento.Domain.Identity;
using Estacionamento.Domain.Operations;

namespace Estacionamento.Application.Operations;

public sealed class AttendanceService(
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork,
    IParkingSpotRepository parkingSpots,
    ITariffRepository tariffs,
    IParkingSessionRepository sessions,
    IAuditRepository audits,
    IOperationsQueries queries,
    IDocumentProtector documents,
    IPublicIdGenerator publicIds,
    IdempotentOperationExecutor idempotent)
{
    public Task<AttendanceCreatedDto> CheckInAsync(CheckInCommand command, CancellationToken cancellationToken)
    {
        AccessGuard.RequireCashierOrAdministrator(currentUser);

        return idempotent.ExecuteAsync(
            "check-in",
            command.IdempotencyKey,
            command,
            201,
            async token =>
            {
                var spot = await parkingSpots.FindByIdAsync(command.SpotId, token)
                    ?? throw DomainException.NotFound("Vaga não encontrada.");
                if (!spot.IsActive)
                {
                    throw DomainException.RuleViolation("A vaga está inativa.");
                }

                if (await parkingSpots.HasOpenOccupancyAsync(spot.Id, token))
                {
                    throw DomainException.Conflict("A vaga já está ocupada.");
                }

                var now = clock.UtcNow;
                var tariff = await tariffs.GetCurrentAsync(now, token)
                    ?? throw DomainException.RuleViolation("Não existe tarifa vigente para concluir a entrada.");

                var publicId = await GenerateUniquePublicIdAsync(token);
                var session = ParkingSession.Create(
                    publicId,
                    spot.Id,
                    command.Plate,
                    command.Model,
                    command.HolderName,
                    documents.Protect(command.HolderDocument),
                    now,
                    currentUser.UserId,
                    tariff.DailyRate);

                session.Occupy(currentUser.UserId, now);
                session.AddInspection(InspectionType.Entry, command.EntryInspection, currentUser.UserId, now);

                await sessions.AddAsync(session, token);
                await audits.AddAsync(
                    AuditEntry.Create(session.Id, currentUser.UserId, "CheckInCompleted",
                        $"Atendimento {session.PublicId} na vaga {spot.Code}", now),
                    token);

                return new AttendanceCreatedDto(
                    session.Id,
                    session.PublicId,
                    session.State.ToString(),
                    spot.Code,
                    session.EntryAtUtc);
            },
            cancellationToken);
    }

    public async Task<PagedResult<AttendanceSummaryDto>> SearchAsync(
        string? plate,
        string? publicId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        AccessGuard.RequireAuthenticatedRole(currentUser);
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 50);
        var normalizedPlate = string.IsNullOrWhiteSpace(plate) ? null : PlateNormalizer.Normalize(plate);
        var normalizedPublicId = string.IsNullOrWhiteSpace(publicId)
            ? null
            : publicId.Trim().ToUpperInvariant();

        return await queries.SearchAttendancesAsync(
            normalizedPlate,
            normalizedPublicId,
            safePage,
            safePageSize,
            CanViewFinancial,
            cancellationToken);
    }

    public async Task<AttendanceDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        AccessGuard.RequireAuthenticatedRole(currentUser);

        var detail = await queries.GetAttendanceAsync(
            id,
            CanViewFinancial,
            CanViewFinancial,
            cancellationToken) ?? throw DomainException.NotFound("Atendimento não encontrado.");

        if (CanViewFinancial)
        {
            await unitOfWork.ExecuteAsync(async token =>
            {
                await audits.AddAsync(
                    AuditEntry.Create(id, currentUser.UserId, "DocumentRead", null, clock.UtcNow),
                    token);
                return true;
            }, cancellationToken);
        }

        return detail;
    }

    public async Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(Guid id, CancellationToken cancellationToken)
    {
        AccessGuard.RequireAdministrator(currentUser);
        return await queries.GetAuditAsync(id, cancellationToken)
            ?? throw DomainException.NotFound("Atendimento não encontrado.");
    }

    private bool CanViewFinancial
        => currentUser.IsInRole(EmployeeRoles.Cashier) || currentUser.IsInRole(EmployeeRoles.Administrator);

    private async Task<string> GenerateUniquePublicIdAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = publicIds.NewPublicId();
            if (!await sessions.PublicIdExistsAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Não foi possível gerar identificador público único.");
    }
}
