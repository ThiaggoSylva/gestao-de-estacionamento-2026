using Estacionamento.Application.Common;
using Estacionamento.Domain.Common;
using Estacionamento.Domain.Identity;
using Estacionamento.Domain.Operations;

namespace Estacionamento.Application.Operations;

public sealed class WithdrawalService(
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork,
    IParkingSessionRepository sessions,
    IWithdrawalRepository withdrawals,
    IChargeRepository charges,
    IAuditRepository audits,
    IOperationsQueries queries,
    IdempotentOperationExecutor idempotent)
{
    public Task<WithdrawalResultDto> RequestAsync(
        RequestWithdrawalCommand command,
        CancellationToken cancellationToken)
    {
        AccessGuard.RequireCashierOrAdministrator(currentUser);

        return idempotent.ExecuteAsync(
            "withdrawal-request",
            command.IdempotencyKey,
            command,
            201,
            async token =>
            {
                var session = await sessions.FindByIdAsync(command.AttendanceId, token)
                    ?? throw DomainException.NotFound("Atendimento não encontrado.");
                if (session.State != ParkingSessionState.Active)
                {
                    throw DomainException.Conflict("O atendimento não está ativo para receber pedido.");
                }

                if (await withdrawals.FindBySessionIdAsync(session.Id, token) is not null)
                {
                    throw DomainException.Conflict("O atendimento já possui pedido de retirada.");
                }

                var now = clock.UtcNow;
                var request = WithdrawalRequest.Create(
                    session.Id,
                    now,
                    currentUser.UserId,
                    command.DocumentVerified ? DocumentVerification.Approved : DocumentVerification.Divergent,
                    command.VerificationNotes);
                var charge = Charge.Fix(
                    session.Id,
                    request.Id,
                    session.EntryAtUtc,
                    now,
                    session.CapturedDailyRate,
                    now);

                session.MarkAwaitingWithdrawal();
                await withdrawals.AddAsync(request, token);
                await charges.AddAsync(charge, token);
                await audits.AddAsync(
                    AuditEntry.Create(session.Id, currentUser.UserId, "WithdrawalRequested",
                        $"Atendimento {session.PublicId}; conferência {request.DocumentVerification}", now),
                    token);

                return new WithdrawalResultDto(
                    session.Id,
                    request.Id,
                    session.PublicId,
                    request.State.ToString(),
                    request.RequestedAtUtc,
                    charge.DailyCount,
                    charge.TotalAmount,
                    charge.TotalAmount);
            },
            cancellationToken);
    }

    public async Task<WithdrawalStateDto> StartSearchAsync(
        StartSearchCommand command,
        CancellationToken cancellationToken)
    {
        AccessGuard.RequireAuthenticatedRole(currentUser);

        return await unitOfWork.ExecuteAsync(async token =>
        {
            var request = await withdrawals.FindByIdAsync(command.WithdrawalId, token)
                ?? throw DomainException.NotFound("Pedido de retirada não encontrado.");
            var session = await sessions.FindByIdAsync(request.ParkingSessionId, token)
                ?? throw DomainException.NotFound("Atendimento não encontrado.");

            var now = clock.UtcNow;
            request.StartSearch(currentUser.UserId, now);
            session.MarkSearching();
            await audits.AddAsync(
                AuditEntry.Create(session.Id, currentUser.UserId, "WithdrawalSearchStarted",
                    $"Atendimento {session.PublicId}", now),
                token);

            return new WithdrawalStateDto(
                request.Id,
                session.Id,
                request.State.ToString(),
                request.SearchStartedAtUtc);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<QueueItemDto>> GetQueueAsync(CancellationToken cancellationToken)
    {
        AccessGuard.RequireAuthenticatedRole(currentUser);
        var includeFinancial = currentUser.IsInRole(EmployeeRoles.Cashier)
            || currentUser.IsInRole(EmployeeRoles.Administrator);
        return queries.GetQueueAsync(includeFinancial, cancellationToken);
    }
}
