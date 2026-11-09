using Estacionamento.Domain.Common;

namespace Estacionamento.Domain.Operations;

public sealed class Inspection
{
    public const int MaxTextLength = 2000;

    private Inspection()
    {
    }

    public Guid Id { get; private set; }
    public Guid ParkingSessionId { get; private set; }
    public InspectionType Type { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public static Inspection Create(
        Guid parkingSessionId,
        InspectionType type,
        string text,
        Guid createdByUserId,
        DateTime createdAtUtc)
    {
        var normalized = (text ?? string.Empty).Trim();
        if (normalized.Length is < 1 or > MaxTextLength)
        {
            throw DomainException.Validation($"A vistoria textual deve conter entre 1 e {MaxTextLength} caracteres.");
        }

        return new Inspection
        {
            Id = Guid.NewGuid(),
            ParkingSessionId = parkingSessionId,
            Type = type,
            Text = normalized,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = createdAtUtc
        };
    }
}
