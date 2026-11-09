using Estacionamento.Domain.Common;

namespace Estacionamento.Domain.Operations;

public sealed class Adjustment
{
    private Adjustment()
    {
    }

    public Guid Id { get; private set; }
    public Guid ParkingSessionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public static Adjustment Create(
        Guid parkingSessionId,
        decimal amount,
        string reason,
        DateTime createdAtUtc,
        Guid createdByUserId)
    {
        if (amount == 0)
        {
            throw DomainException.Validation("O ajuste deve ter valor diferente de zero.");
        }

        if (!Money.HasSupportedScale(amount))
        {
            throw DomainException.Validation("O ajuste deve ter no máximo duas casas decimais.");
        }

        var normalizedReason = (reason ?? string.Empty).Trim();
        if (normalizedReason.Length is < 5 or > 500)
        {
            throw DomainException.Validation("O motivo do ajuste deve conter entre 5 e 500 caracteres.");
        }

        return new Adjustment
        {
            Id = Guid.NewGuid(),
            ParkingSessionId = parkingSessionId,
            Amount = Money.Normalize(amount),
            Reason = normalizedReason,
            CreatedAtUtc = createdAtUtc,
            CreatedByUserId = createdByUserId
        };
    }
}
