using Estacionamento.Domain.Common;

namespace Estacionamento.Domain.Operations;

public sealed class Tariff
{
    private Tariff()
    {
    }

    public Guid Id { get; private set; }
    public decimal DailyRate { get; private set; }
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public static Tariff Create(decimal dailyRate, DateTime effectiveFromUtc, DateTime createdAtUtc, Guid createdByUserId)
    {
        if (dailyRate <= 0)
        {
            throw DomainException.Validation("A tarifa diária deve ser maior que zero.");
        }

        if (!Money.HasSupportedScale(dailyRate))
        {
            throw DomainException.Validation("A tarifa diária deve ter no máximo duas casas decimais.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw DomainException.Validation("O responsável pela tarifa é obrigatório.");
        }

        return new Tariff
        {
            Id = Guid.NewGuid(),
            DailyRate = Money.Normalize(dailyRate),
            EffectiveFromUtc = effectiveFromUtc,
            CreatedAtUtc = createdAtUtc,
            CreatedByUserId = createdByUserId
        };
    }
}
