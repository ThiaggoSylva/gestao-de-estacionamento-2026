using Estacionamento.Domain.Common;

namespace Estacionamento.Domain.Operations;

public sealed class Occupancy
{
    private Occupancy()
    {
    }

    public Guid Id { get; private set; }
    public Guid ParkingSpotId { get; private set; }
    public ParkingSpot? ParkingSpot { get; private set; }
    public Guid ParkingSessionId { get; private set; }
    public ParkingSession? ParkingSession { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public Guid StartedByUserId { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public Guid? EndedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsOpen => EndedAtUtc is null;

    public static Occupancy Start(Guid parkingSpotId, Guid parkingSessionId, Guid startedByUserId, DateTime startedAtUtc)
    {
        if (parkingSpotId == Guid.Empty || parkingSessionId == Guid.Empty || startedByUserId == Guid.Empty)
        {
            throw DomainException.Validation("Vaga, atendimento e operador são obrigatórios para ocupação.");
        }

        return new Occupancy
        {
            Id = Guid.NewGuid(),
            ParkingSpotId = parkingSpotId,
            ParkingSessionId = parkingSessionId,
            StartedAtUtc = startedAtUtc,
            StartedByUserId = startedByUserId
        };
    }

    public void End(Guid endedByUserId, DateTime endedAtUtc)
    {
        if (!IsOpen)
        {
            throw DomainException.Conflict("A ocupação da vaga já foi encerrada.");
        }

        EndedAtUtc = endedAtUtc;
        EndedByUserId = endedByUserId;
    }
}
