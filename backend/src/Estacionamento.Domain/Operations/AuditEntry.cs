namespace Estacionamento.Domain.Operations;

public sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    public Guid Id { get; private set; }
    public Guid? ParkingSessionId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Details { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static AuditEntry Create(
        Guid? parkingSessionId,
        Guid actorUserId,
        string action,
        string? details,
        DateTime occurredAtUtc)
    {
        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            ParkingSessionId = parkingSessionId,
            ActorUserId = actorUserId,
            Action = action,
            Details = details,
            OccurredAtUtc = occurredAtUtc
        };
    }
}
