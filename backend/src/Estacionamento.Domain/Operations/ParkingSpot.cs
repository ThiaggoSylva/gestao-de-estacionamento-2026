using Estacionamento.Domain.Common;

namespace Estacionamento.Domain.Operations;

public sealed class ParkingSpot
{
    private ParkingSpot()
    {
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public byte[] RowVersion { get; private set; } = [];
    public ICollection<Occupancy> Occupancies { get; private set; } = [];

    public static ParkingSpot Create(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length is < 1 or > 20)
        {
            throw DomainException.Validation("O código da vaga deve conter entre 1 e 20 caracteres.");
        }

        return new ParkingSpot
        {
            Id = Guid.NewGuid(),
            Code = normalized
        };
    }

    public void Deactivate() => IsActive = false;
}
