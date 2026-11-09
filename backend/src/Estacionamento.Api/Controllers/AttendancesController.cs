using Estacionamento.Application.Operations;
using Estacionamento.Domain.Identity;
using Estacionamento.Domain.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estacionamento.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/attendances")]
public sealed class AttendancesController(
    AttendanceService attendance,
    WithdrawalService withdrawal,
    SettlementService settlement) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = EmployeeRoles.CashierOrAdministrator)]
    public async Task<ActionResult<AttendanceCreatedDto>> CheckIn(
        CheckInRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await attendance.CheckInAsync(
            new CheckInCommand(
                request.SpotId,
                request.Plate,
                request.Model,
                request.HolderName,
                request.HolderDocument,
                request.EntryInspection,
                idempotencyKey ?? string.Empty),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AttendanceSummaryDto>>> Search(
        [FromQuery] string? plate,
        [FromQuery] string? publicId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await attendance.SearchAsync(plate, publicId, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AttendanceDetailDto>> GetDetail(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await attendance.GetDetailAsync(id, cancellationToken));

    [HttpGet("{id:guid}/audit")]
    [Authorize(Roles = EmployeeRoles.Administrator)]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> GetAudit(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await attendance.GetAuditAsync(id, cancellationToken));

    [HttpPost("{id:guid}/withdrawals")]
    [Authorize(Roles = EmployeeRoles.CashierOrAdministrator)]
    public async Task<ActionResult<WithdrawalResultDto>> RequestWithdrawal(
        Guid id,
        RequestWithdrawalRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await withdrawal.RequestAsync(
            new RequestWithdrawalCommand(
                id,
                request.DocumentVerified,
                request.VerificationNotes,
                idempotencyKey ?? string.Empty),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = EmployeeRoles.CashierOrAdministrator)]
    public async Task<ActionResult<PaymentResultDto>> RegisterPayment(
        Guid id,
        RegisterPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await settlement.PayAsync(
            new RegisterPaymentCommand(
                id,
                request.Amount,
                request.Method,
                idempotencyKey ?? string.Empty),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{id:guid}/adjustments")]
    [Authorize(Roles = EmployeeRoles.Administrator)]
    public async Task<ActionResult<AdjustmentResultDto>> RegisterAdjustment(
        Guid id,
        RegisterAdjustmentRequest request,
        CancellationToken cancellationToken)
        => Ok(await settlement.AdjustAsync(
            new RegisterAdjustmentCommand(id, request.Amount, request.Reason),
            cancellationToken));

    [HttpPost("{id:guid}/authorizations")]
    [Authorize(Roles = EmployeeRoles.Administrator)]
    public async Task<ActionResult<AuthorizationResultDto>> AuthorizeException(
        Guid id,
        AuthorizeExceptionRequest request,
        CancellationToken cancellationToken)
        => Ok(await settlement.AuthorizeAsync(
            new AuthorizeExceptionCommand(id, request.Type, request.Reason),
            cancellationToken));

    [HttpPost("{id:guid}/delivery")]
    [Authorize(Roles = EmployeeRoles.CashierOrAdministrator)]
    public async Task<ActionResult<DeliveryResultDto>> Deliver(
        Guid id,
        DeliverRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => Ok(await settlement.DeliverAsync(
            new DeliverCommand(id, request.ExitInspection, idempotencyKey ?? string.Empty),
            cancellationToken));
}

public sealed record CheckInRequest(
    Guid SpotId,
    string Plate,
    string Model,
    string HolderName,
    string HolderDocument,
    string EntryInspection);

public sealed record RequestWithdrawalRequest(bool DocumentVerified, string? VerificationNotes);

public sealed record RegisterPaymentRequest(decimal Amount, PaymentMethod Method);

public sealed record RegisterAdjustmentRequest(decimal Amount, string Reason);

public sealed record AuthorizeExceptionRequest(ExceptionAuthorizationType Type, string Reason);

public sealed record DeliverRequest(string ExitInspection);
