using Estacionamento.Application.Common;
using Estacionamento.Domain.Common;
using Estacionamento.Domain.Operations;

namespace Estacionamento.Application.Operations;

public sealed class SettlementService(
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork,
    IParkingSessionRepository sessions,
    IPaymentRepository payments,
    IAdjustmentRepository adjustments,
    IAuthorizationRepository authorizations,
    IAuditRepository audits,
    IdempotentOperationExecutor idempotent)
{
    public Task<PaymentResultDto> PayAsync(
        RegisterPaymentCommand command,
        CancellationToken cancellationToken)
    {
        AccessGuard.RequireCashierOrAdministrator(currentUser);

        return idempotent.ExecuteAsync(
            "payment",
            command.IdempotencyKey,
            command,
            201,
            async token =>
            {
                var session = await LoadSessionAsync(command.AttendanceId, token);
                var (charged, paid, outstanding) = BalanceCalculator.Calculate(session);
                if (outstanding <= 0)
                {
                    throw DomainException.RuleViolation("O atendimento já está quitado.");
                }

                if (Money.Normalize(command.Amount) != outstanding)
                {
                    throw DomainException.RuleViolation(
                        "O pagamento deve quitar integralmente o saldo exigível.");
                }

                var now = clock.UtcNow;
                var payment = Payment.Create(
                    session.Id,
                    command.Amount,
                    command.Method,
                    now,
                    currentUser.UserId);
                await payments.AddAsync(payment, token);
                await audits.AddAsync(
                    AuditEntry.Create(session.Id, currentUser.UserId, "PaymentRegistered",
                        $"Atendimento {session.PublicId}; forma {payment.Method}", now),
                    token);

                var newPaid = Money.Normalize(paid + payment.Amount);
                return new PaymentResultDto(
                    payment.Id,
                    payment.Amount,
                    payment.Method.ToString(),
                    charged,
                    newPaid,
                    Money.Normalize(charged - newPaid));
            },
            cancellationToken);
    }

    public async Task<AdjustmentResultDto> AdjustAsync(
        RegisterAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        AccessGuard.RequireAdministrator(currentUser);

        return await unitOfWork.ExecuteAsync(async token =>
        {
            var session = await LoadSessionAsync(command.AttendanceId, token);
            if (!session.Charges.Any())
            {
                throw DomainException.RuleViolation("O atendimento ainda não possui cobrança fixada.");
            }

            var (chargedBefore, paid, _) = BalanceCalculator.Calculate(session);
            var now = clock.UtcNow;
            var adjustment = Adjustment.Create(
                session.Id,
                command.Amount,
                command.Reason,
                now,
                currentUser.UserId);
            await adjustments.AddAsync(adjustment, token);
            await audits.AddAsync(
                AuditEntry.Create(session.Id, currentUser.UserId, "AdjustmentRegistered",
                    $"Atendimento {session.PublicId}; valor {adjustment.Amount:0.00}", now),
                token);

            var charged = Money.Normalize(chargedBefore + adjustment.Amount);
            var outstanding = Money.Normalize(charged - paid);
            return new AdjustmentResultDto(adjustment.Id, adjustment.Amount, charged, paid, outstanding);
        }, cancellationToken);
    }

    public async Task<AuthorizationResultDto> AuthorizeAsync(
        AuthorizeExceptionCommand command,
        CancellationToken cancellationToken)
    {
        AccessGuard.RequireAdministrator(currentUser);

        return await unitOfWork.ExecuteAsync(async token =>
        {
            var session = await LoadSessionAsync(command.AttendanceId, token);
            if (session.State == ParkingSessionState.Delivered)
            {
                throw DomainException.Conflict("Atendimento entregue não aceita nova autorização.");
            }

            if (await authorizations.ExistsAsync(session.Id, command.Type, token))
            {
                throw DomainException.Conflict("Já existe autorização deste tipo para o atendimento.");
            }

            var now = clock.UtcNow;
            var authorization = ExceptionAuthorization.Create(
                session.Id,
                command.Type,
                command.Reason,
                now,
                currentUser.UserId);
            await authorizations.AddAsync(authorization, token);
            await audits.AddAsync(
                AuditEntry.Create(session.Id, currentUser.UserId, "ExceptionAuthorized",
                    $"Atendimento {session.PublicId}; tipo {authorization.Type}", now),
                token);

            return new AuthorizationResultDto(
                authorization.Id,
                authorization.Type.ToString(),
                authorization.Reason,
                authorization.CreatedAtUtc);
        }, cancellationToken);
    }

    public Task<DeliveryResultDto> DeliverAsync(
        DeliverCommand command,
        CancellationToken cancellationToken)
    {
        AccessGuard.RequireCashierOrAdministrator(currentUser);

        return idempotent.ExecuteAsync(
            "delivery",
            command.IdempotencyKey,
            command,
            200,
            async token =>
            {
                var session = await LoadSessionAsync(command.AttendanceId, token);
                if (session.State == ParkingSessionState.Delivered)
                {
                    throw DomainException.Conflict("O atendimento já foi entregue.");
                }

                var withdrawal = session.WithdrawalRequests.SingleOrDefault()
                    ?? throw DomainException.RuleViolation("A entrega exige pedido de retirada prévio.");
                if (session.Inspections.Any(inspection => inspection.Type == InspectionType.Exit))
                {
                    throw DomainException.Conflict("Já existe vistoria de saída para o atendimento.");
                }

                var hasDocumentAuthorization = session.Authorizations
                    .Any(authorization => authorization.Type == ExceptionAuthorizationType.DocumentDivergence);
                if (withdrawal.DocumentVerification == DocumentVerification.Divergent
                    && !hasDocumentAuthorization)
                {
                    throw DomainException.RuleViolation(
                        "Divergência documental exige autorização excepcional do administrador.");
                }

                var (_, _, outstanding) = BalanceCalculator.Calculate(session);
                var hasBalanceAuthorization = session.Authorizations
                    .Any(authorization => authorization.Type == ExceptionAuthorizationType.OutstandingBalance);
                if (outstanding > 0 && !hasBalanceAuthorization)
                {
                    throw DomainException.RuleViolation(
                        "Saldo pendente exige pagamento integral ou autorização excepcional.");
                }

                var occupancy = session.Occupancies.SingleOrDefault(occupancy => occupancy.IsOpen)
                    ?? throw DomainException.RuleViolation("A vaga do atendimento não está ocupada.");

                var now = clock.UtcNow;
                session.AddInspection(InspectionType.Exit, command.ExitInspection, currentUser.UserId, now);
                withdrawal.MarkDelivered(now);
                session.MarkDelivered(now, currentUser.UserId);
                occupancy.End(currentUser.UserId, now);
                await audits.AddAsync(
                    AuditEntry.Create(session.Id, currentUser.UserId, "DeliveryCompleted",
                        $"Atendimento {session.PublicId}; vaga {occupancy.ParkingSpotId}", now),
                    token);

                return new DeliveryResultDto(
                    session.Id,
                    session.PublicId,
                    session.State.ToString(),
                    session.DeliveredAtUtc!.Value,
                    session.ParkingSpot?.Code ?? string.Empty,
                    outstanding);
            },
            cancellationToken);
    }

    private async Task<ParkingSession> LoadSessionAsync(Guid id, CancellationToken cancellationToken)
        => await sessions.FindWithOperationsByIdAsync(id, cancellationToken)
            ?? throw DomainException.NotFound("Atendimento não encontrado.");
}
