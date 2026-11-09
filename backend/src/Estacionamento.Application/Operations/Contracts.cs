using Estacionamento.Domain.Operations;

namespace Estacionamento.Application.Operations;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record SpotDto(
    Guid Id,
    string Code,
    bool IsActive,
    bool IsOccupied,
    string? SessionPublicId,
    string? Plate);

public sealed record TariffDto(Guid Id, decimal DailyRate, DateTime EffectiveFromUtc);

public sealed record AttendanceSummaryDto(
    Guid Id,
    string PublicId,
    string Plate,
    string Model,
    string State,
    string SpotCode,
    DateTime EntryAtUtc,
    DateTime? RequestedAtUtc,
    DateTime? DeliveredAtUtc,
    string Document,
    decimal? ChargedAmount,
    decimal? PaidAmount,
    decimal? OutstandingAmount);

public sealed record WithdrawalDetailDto(
    Guid Id,
    string State,
    DateTime RequestedAtUtc,
    string DocumentVerification,
    string? VerificationNotes,
    DateTime? SearchStartedAtUtc,
    int DailyCount,
    decimal? TotalAmount);

public sealed record FinancialDto(decimal ChargedAmount, decimal PaidAmount, decimal OutstandingAmount);

public sealed record PaymentDto(
    Guid Id,
    decimal Amount,
    string Method,
    DateTime PaidAtUtc,
    Guid CreatedByUserId,
    string? CreatedByUserName);

public sealed record AdjustmentDto(
    Guid Id,
    decimal Amount,
    string Reason,
    DateTime CreatedAtUtc,
    Guid CreatedByUserId,
    string? CreatedByUserName);

public sealed record AuthorizationDto(
    Guid Id,
    string Type,
    string Reason,
    DateTime CreatedAtUtc,
    Guid CreatedByUserId,
    string? CreatedByUserName);

public sealed record AttendanceDetailDto(
    Guid Id,
    string PublicId,
    string Plate,
    string Model,
    string HolderName,
    string Document,
    string State,
    Guid SpotId,
    string SpotCode,
    DateTime EntryAtUtc,
    DateTime? DeliveredAtUtc,
    string EntryInspection,
    string? ExitInspection,
    WithdrawalDetailDto? Withdrawal,
    FinancialDto? Financial,
    IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<AdjustmentDto> Adjustments,
    IReadOnlyList<AuthorizationDto> Authorizations,
    bool CanRequestWithdrawal,
    bool CanStartSearch,
    bool CanDeliver);

public sealed record QueueItemDto(
    Guid RequestId,
    Guid AttendanceId,
    string PublicId,
    string Plate,
    string Model,
    string SpotCode,
    string State,
    DateTime RequestedAtUtc,
    DateTime? SearchStartedAtUtc,
    string DocumentVerification,
    int DailyCount,
    decimal? TotalAmount,
    decimal? OutstandingAmount,
    string? RequestedByUserName,
    string? SearchStartedByUserName,
    bool CanStartSearch);

public sealed record AuditEntryDto(
    Guid Id,
    string Action,
    string? Details,
    DateTime OccurredAtUtc,
    Guid ActorUserId,
    string? ActorUserName);

public sealed record AttendanceCreatedDto(
    Guid Id,
    string PublicId,
    string State,
    string SpotCode,
    DateTime EntryAtUtc);

public sealed record WithdrawalResultDto(
    Guid AttendanceId,
    Guid RequestId,
    string PublicId,
    string State,
    DateTime RequestedAtUtc,
    int DailyCount,
    decimal TotalAmount,
    decimal OutstandingAmount);

public sealed record WithdrawalStateDto(
    Guid RequestId,
    Guid AttendanceId,
    string State,
    DateTime? SearchStartedAtUtc);

public sealed record PaymentResultDto(
    Guid PaymentId,
    decimal Amount,
    string Method,
    decimal ChargedAmount,
    decimal PaidAmount,
    decimal OutstandingAmount);

public sealed record AdjustmentResultDto(
    Guid AdjustmentId,
    decimal Amount,
    decimal ChargedAmount,
    decimal PaidAmount,
    decimal OutstandingAmount);

public sealed record AuthorizationResultDto(
    Guid Id,
    string Type,
    string Reason,
    DateTime CreatedAtUtc);

public sealed record DeliveryResultDto(
    Guid AttendanceId,
    string PublicId,
    string State,
    DateTime DeliveredAtUtc,
    string SpotCode,
    decimal OutstandingAmount);

public sealed record CreateSpotCommand(string Code);

public sealed record SetTariffCommand(decimal DailyRate);

public sealed record CheckInCommand(
    Guid SpotId,
    string Plate,
    string Model,
    string HolderName,
    string HolderDocument,
    string EntryInspection,
    string IdempotencyKey);

public sealed record RequestWithdrawalCommand(
    Guid AttendanceId,
    bool DocumentVerified,
    string? VerificationNotes,
    string IdempotencyKey);

public sealed record StartSearchCommand(Guid WithdrawalId);

public sealed record RegisterPaymentCommand(
    Guid AttendanceId,
    decimal Amount,
    PaymentMethod Method,
    string IdempotencyKey);

public sealed record RegisterAdjustmentCommand(
    Guid AttendanceId,
    decimal Amount,
    string Reason);

public sealed record AuthorizeExceptionCommand(
    Guid AttendanceId,
    ExceptionAuthorizationType Type,
    string Reason);

public sealed record DeliverCommand(
    Guid AttendanceId,
    string ExitInspection,
    string IdempotencyKey);
