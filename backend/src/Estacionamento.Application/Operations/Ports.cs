using Estacionamento.Domain.Operations;

namespace Estacionamento.Application.Operations;

public interface IOperationsQueries
{
    Task<IReadOnlyList<SpotDto>> GetSpotsAsync(CancellationToken cancellationToken);
    Task<TariffDto?> GetCurrentTariffAsync(CancellationToken cancellationToken);
    Task<PagedResult<AttendanceSummaryDto>> SearchAttendancesAsync(
        string? plate,
        string? publicId,
        int page,
        int pageSize,
        bool includeFinancial,
        CancellationToken cancellationToken);
    Task<AttendanceDetailDto?> GetAttendanceAsync(
        Guid id,
        bool includeFinancial,
        bool includeFullDocument,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<QueueItemDto>> GetQueueAsync(bool includeFinancial, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditEntryDto>?> GetAuditAsync(Guid attendanceId, CancellationToken cancellationToken);
}

public interface IParkingSpotRepository
{
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<ParkingSpot?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(ParkingSpot spot, CancellationToken cancellationToken);
    Task<bool> HasOpenOccupancyAsync(Guid parkingSpotId, CancellationToken cancellationToken);
}

public interface ITariffRepository
{
    Task<Tariff?> GetCurrentAsync(DateTime atUtc, CancellationToken cancellationToken);
    Task AddAsync(Tariff tariff, CancellationToken cancellationToken);
}

public interface IParkingSessionRepository
{
    Task AddAsync(ParkingSession session, CancellationToken cancellationToken);
    Task<bool> PublicIdExistsAsync(string publicId, CancellationToken cancellationToken);
    Task<ParkingSession?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ParkingSession?> FindWithOperationsByIdAsync(Guid id, CancellationToken cancellationToken);
}

public interface IWithdrawalRepository
{
    Task AddAsync(WithdrawalRequest request, CancellationToken cancellationToken);
    Task<WithdrawalRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<WithdrawalRequest?> FindBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken);
}

public interface IChargeRepository
{
    Task AddAsync(Charge charge, CancellationToken cancellationToken);
}

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
}

public interface IAdjustmentRepository
{
    Task AddAsync(Adjustment adjustment, CancellationToken cancellationToken);
}

public interface IAuthorizationRepository
{
    Task AddAsync(ExceptionAuthorization authorization, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid sessionId, ExceptionAuthorizationType type, CancellationToken cancellationToken);
}

public interface IAuditRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken cancellationToken);
}

public interface IPublicIdGenerator
{
    string NewPublicId();
}
