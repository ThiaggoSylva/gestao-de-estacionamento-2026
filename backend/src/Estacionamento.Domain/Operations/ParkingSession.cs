using Estacionamento.Domain.Common;

namespace Estacionamento.Domain.Operations;

public sealed class ParkingSession
{
    private ParkingSession()
    {
    }

    public Guid Id { get; private set; }
    public string PublicId { get; private set; } = string.Empty;
    public Guid ParkingSpotId { get; private set; }
    public ParkingSpot? ParkingSpot { get; private set; }
    public string Plate { get; private set; } = string.Empty;
    public string PlateNormalized { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string HolderName { get; private set; } = string.Empty;
    public string HolderDocumentProtected { get; private set; } = string.Empty;
    public DateTime EntryAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public decimal CapturedDailyRate { get; private set; }
    public ParkingSessionState State { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public Guid? DeliveredByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public ICollection<Inspection> Inspections { get; private set; } = [];
    public ICollection<Occupancy> Occupancies { get; private set; } = [];
    public ICollection<WithdrawalRequest> WithdrawalRequests { get; private set; } = [];
    public ICollection<Charge> Charges { get; private set; } = [];
    public ICollection<Payment> Payments { get; private set; } = [];
    public ICollection<Adjustment> Adjustments { get; private set; } = [];
    public ICollection<ExceptionAuthorization> Authorizations { get; private set; } = [];

    public static ParkingSession Create(
        string publicId,
        Guid parkingSpotId,
        string plate,
        string model,
        string holderName,
        string holderDocumentProtected,
        DateTime entryAtUtc,
        Guid createdByUserId,
        decimal capturedDailyRate)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            throw DomainException.Validation("O identificador público é obrigatório.");
        }

        var normalizedModel = (model ?? string.Empty).Trim();
        if (normalizedModel.Length is < 1 or > 120)
        {
            throw DomainException.Validation("O modelo deve conter entre 1 e 120 caracteres.");
        }

        var normalizedHolder = (holderName ?? string.Empty).Trim();
        if (normalizedHolder.Length is < 1 or > 160)
        {
            throw DomainException.Validation("O nome do titular deve conter entre 1 e 160 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(holderDocumentProtected))
        {
            throw DomainException.Validation("O documento do titular é obrigatório.");
        }

        if (capturedDailyRate <= 0)
        {
            throw DomainException.Validation("A tarifa capturada deve ser maior que zero.");
        }

        return new ParkingSession
        {
            Id = Guid.NewGuid(),
            PublicId = publicId.Trim(),
            ParkingSpotId = parkingSpotId,
            Plate = PlateNormalizer.Normalize(plate),
            PlateNormalized = PlateNormalizer.Normalize(plate),
            Model = normalizedModel,
            HolderName = normalizedHolder,
            HolderDocumentProtected = holderDocumentProtected,
            EntryAtUtc = entryAtUtc,
            CreatedByUserId = createdByUserId,
            CapturedDailyRate = Money.Normalize(capturedDailyRate),
            State = ParkingSessionState.Active
        };
    }

    public Occupancy Occupy(Guid actorUserId, DateTime atUtc)
    {
        var occupancy = Occupancy.Start(ParkingSpotId, Id, actorUserId, atUtc);
        Occupancies.Add(occupancy);
        return occupancy;
    }

    public Inspection AddInspection(InspectionType type, string text, Guid actorUserId, DateTime atUtc)
    {
        if (State == ParkingSessionState.Delivered)
        {
            throw DomainException.Conflict("Atendimento entregue não aceita nova vistoria.");
        }

        if (Inspections.Any(inspection => inspection.Type == type))
        {
            throw DomainException.Conflict("Já existe vistoria deste tipo para o atendimento.");
        }

        var inspection = Inspection.Create(Id, type, text, actorUserId, atUtc);
        Inspections.Add(inspection);
        return inspection;
    }

    public void MarkAwaitingWithdrawal()
    {
        if (State != ParkingSessionState.Active)
        {
            throw DomainException.Conflict("Somente atendimento ativo pode receber pedido de retirada.");
        }

        State = ParkingSessionState.AwaitingWithdrawal;
    }

    public void MarkSearching()
    {
        if (State != ParkingSessionState.AwaitingWithdrawal)
        {
            throw DomainException.Conflict("Somente atendimento aguardando retirada pode iniciar busca.");
        }

        State = ParkingSessionState.Searching;
    }

    public void MarkDelivered(DateTime deliveredAtUtc, Guid deliveredByUserId)
    {
        if (State == ParkingSessionState.Delivered)
        {
            throw DomainException.Conflict("O atendimento já foi entregue.");
        }

        if (State is not (ParkingSessionState.AwaitingWithdrawal or ParkingSessionState.Searching))
        {
            throw DomainException.Conflict(
                "Somente atendimento aguardando retirada ou em busca pode ser entregue.");
        }

        State = ParkingSessionState.Delivered;
        DeliveredAtUtc = deliveredAtUtc;
        DeliveredByUserId = deliveredByUserId;
    }
}
