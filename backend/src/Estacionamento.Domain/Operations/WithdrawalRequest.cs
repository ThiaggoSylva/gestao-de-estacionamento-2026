using Estacionamento.Domain.Common;

namespace Estacionamento.Domain.Operations;

public sealed class WithdrawalRequest
{
    private WithdrawalRequest()
    {
    }

    public Guid Id { get; private set; }
    public Guid ParkingSessionId { get; private set; }
    public ParkingSession? ParkingSession { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public DocumentVerification DocumentVerification { get; private set; }
    public string? VerificationNotes { get; private set; }
    public WithdrawalState State { get; private set; }
    public DateTime? SearchStartedAtUtc { get; private set; }
    public Guid? SearchStartedByUserId { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static WithdrawalRequest Create(
        Guid parkingSessionId,
        DateTime requestedAtUtc,
        Guid requestedByUserId,
        DocumentVerification documentVerification,
        string? verificationNotes)
    {
        if (parkingSessionId == Guid.Empty || requestedByUserId == Guid.Empty)
        {
            throw DomainException.Validation("Atendimento e operador do pedido são obrigatórios.");
        }

        var notes = string.IsNullOrWhiteSpace(verificationNotes) ? null : verificationNotes.Trim();
        if (notes is { Length: > 500 })
        {
            throw DomainException.Validation("As observações da conferência devem ter no máximo 500 caracteres.");
        }

        return new WithdrawalRequest
        {
            Id = Guid.NewGuid(),
            ParkingSessionId = parkingSessionId,
            RequestedAtUtc = requestedAtUtc,
            RequestedByUserId = requestedByUserId,
            DocumentVerification = documentVerification,
            VerificationNotes = notes,
            State = WithdrawalState.Awaiting
        };
    }

    public void StartSearch(Guid startedByUserId, DateTime startedAtUtc)
    {
        if (State != WithdrawalState.Awaiting)
        {
            throw DomainException.Conflict("Somente pedido aguardando pode iniciar busca.");
        }

        State = WithdrawalState.Searching;
        SearchStartedByUserId = startedByUserId;
        SearchStartedAtUtc = startedAtUtc;
    }

    public void MarkDelivered(DateTime deliveredAtUtc)
    {
        if (State == WithdrawalState.Delivered)
        {
            throw DomainException.Conflict("O pedido já foi entregue.");
        }

        if (State is not (WithdrawalState.Awaiting or WithdrawalState.Searching))
        {
            throw DomainException.Conflict("O pedido não permite entrega no estado atual.");
        }

        State = WithdrawalState.Delivered;
        DeliveredAtUtc = deliveredAtUtc;
    }
}
