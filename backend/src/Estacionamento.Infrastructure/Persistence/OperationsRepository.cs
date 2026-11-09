using Estacionamento.Application.Operations;
using Estacionamento.Domain.Operations;
using Estacionamento.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Estacionamento.Infrastructure.Persistence;

public sealed class OperationsRepository(IdentityDbContext database)
    : IParkingSpotRepository,
      ITariffRepository,
      IParkingSessionRepository,
      IWithdrawalRepository,
      IChargeRepository,
      IPaymentRepository,
      IAdjustmentRepository,
      IAuthorizationRepository,
      IAuditRepository
{
    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
        => database.ParkingSpots.AnyAsync(spot => spot.Code == code, cancellationToken);

    Task<ParkingSpot?> IParkingSpotRepository.FindByIdAsync(Guid id, CancellationToken cancellationToken)
        => database.ParkingSpots.FirstOrDefaultAsync(spot => spot.Id == id, cancellationToken);

    public async Task AddAsync(ParkingSpot spot, CancellationToken cancellationToken)
        => await database.ParkingSpots.AddAsync(spot, cancellationToken);

    public Task<bool> HasOpenOccupancyAsync(Guid parkingSpotId, CancellationToken cancellationToken)
        => database.Occupancies.AnyAsync(
            occupancy => occupancy.ParkingSpotId == parkingSpotId && occupancy.EndedAtUtc == null,
            cancellationToken);

    public Task<Tariff?> GetCurrentAsync(DateTime atUtc, CancellationToken cancellationToken)
        => database.Tariffs
            .Where(tariff => tariff.EffectiveFromUtc <= atUtc)
            .OrderByDescending(tariff => tariff.EffectiveFromUtc)
            .ThenByDescending(tariff => tariff.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Tariff tariff, CancellationToken cancellationToken)
        => await database.Tariffs.AddAsync(tariff, cancellationToken);

    public async Task AddAsync(ParkingSession session, CancellationToken cancellationToken)
        => await database.ParkingSessions.AddAsync(session, cancellationToken);

    public Task<bool> PublicIdExistsAsync(string publicId, CancellationToken cancellationToken)
        => database.ParkingSessions.AnyAsync(session => session.PublicId == publicId, cancellationToken);

    Task<ParkingSession?> IParkingSessionRepository.FindByIdAsync(Guid id, CancellationToken cancellationToken)
        => database.ParkingSessions
            .Include(session => session.ParkingSpot)
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

    public Task<ParkingSession?> FindWithOperationsByIdAsync(Guid id, CancellationToken cancellationToken)
        => database.ParkingSessions
            .Include(session => session.ParkingSpot)
            .Include(session => session.Inspections)
            .Include(session => session.WithdrawalRequests)
            .Include(session => session.Charges)
            .Include(session => session.Payments)
            .Include(session => session.Adjustments)
            .Include(session => session.Authorizations)
            .Include(session => session.Occupancies)
            .AsSplitQuery()
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

    public async Task AddAsync(WithdrawalRequest request, CancellationToken cancellationToken)
        => await database.WithdrawalRequests.AddAsync(request, cancellationToken);

    Task<WithdrawalRequest?> IWithdrawalRepository.FindByIdAsync(Guid id, CancellationToken cancellationToken)
        => database.WithdrawalRequests.FirstOrDefaultAsync(request => request.Id == id, cancellationToken);

    public Task<WithdrawalRequest?> FindBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken)
        => database.WithdrawalRequests
            .FirstOrDefaultAsync(request => request.ParkingSessionId == sessionId, cancellationToken);

    public async Task AddAsync(Charge charge, CancellationToken cancellationToken)
        => await database.Charges.AddAsync(charge, cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
        => await database.Payments.AddAsync(payment, cancellationToken);

    public async Task AddAsync(Adjustment adjustment, CancellationToken cancellationToken)
        => await database.Adjustments.AddAsync(adjustment, cancellationToken);

    public async Task AddAsync(ExceptionAuthorization authorization, CancellationToken cancellationToken)
        => await database.ExceptionAuthorizations.AddAsync(authorization, cancellationToken);

    public Task<bool> ExistsAsync(
        Guid sessionId,
        ExceptionAuthorizationType type,
        CancellationToken cancellationToken)
        => database.ExceptionAuthorizations.AnyAsync(
            authorization => authorization.ParkingSessionId == sessionId && authorization.Type == type,
            cancellationToken);

    public async Task AddAsync(AuditEntry entry, CancellationToken cancellationToken)
        => await database.AuditEntries.AddAsync(entry, cancellationToken);
}
