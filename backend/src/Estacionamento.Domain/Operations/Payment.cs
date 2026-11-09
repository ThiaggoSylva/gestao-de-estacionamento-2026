using Estacionamento.Domain.Common;

namespace Estacionamento.Domain.Operations;

public sealed class Payment
{
    private Payment()
    {
    }

    public Guid Id { get; private set; }
    public Guid ParkingSessionId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public DateTime PaidAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public static Payment Create(
        Guid parkingSessionId,
        decimal amount,
        PaymentMethod method,
        DateTime paidAtUtc,
        Guid createdByUserId)
    {
        if (amount <= 0)
        {
            throw DomainException.Validation("O valor do pagamento deve ser maior que zero.");
        }

        if (!Money.HasSupportedScale(amount))
        {
            throw DomainException.Validation("O valor do pagamento deve ter no máximo duas casas decimais.");
        }

        if (!Enum.IsDefined(method))
        {
            throw DomainException.Validation("A forma de pagamento é inválida.");
        }

        return new Payment
        {
            Id = Guid.NewGuid(),
            ParkingSessionId = parkingSessionId,
            Amount = Money.Normalize(amount),
            Method = method,
            PaidAtUtc = paidAtUtc,
            CreatedByUserId = createdByUserId
        };
    }
}
