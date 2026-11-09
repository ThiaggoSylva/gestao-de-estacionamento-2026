using Estacionamento.Domain.Common;

namespace Estacionamento.Domain.Operations;

public sealed class ExceptionAuthorization
{
    private ExceptionAuthorization()
    {
    }

    public Guid Id { get; private set; }
    public Guid ParkingSessionId { get; private set; }
    public ExceptionAuthorizationType Type { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public static ExceptionAuthorization Create(
        Guid parkingSessionId,
        ExceptionAuthorizationType type,
        string reason,
        DateTime createdAtUtc,
        Guid createdByUserId)
    {
        if (!Enum.IsDefined(type))
        {
            throw DomainException.Validation("O tipo de autorização é inválido.");
        }

        var normalizedReason = (reason ?? string.Empty).Trim();
        if (normalizedReason.Length is < 5 or > 500)
        {
            throw DomainException.Validation("A justificativa da autorização deve conter entre 5 e 500 caracteres.");
        }

        return new ExceptionAuthorization
        {
            Id = Guid.NewGuid(),
            ParkingSessionId = parkingSessionId,
            Type = type,
            Reason = normalizedReason,
            CreatedAtUtc = createdAtUtc,
            CreatedByUserId = createdByUserId
        };
    }
}
