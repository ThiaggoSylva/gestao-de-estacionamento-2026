using Estacionamento.Domain.Common;

namespace Estacionamento.Domain.Operations;

public static class PlateNormalizer
{
    public static string Normalize(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
        {
            throw DomainException.Validation("A placa é obrigatória.");
        }

        var normalized = new string(plate.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (normalized.Length is < 4 or > 10)
        {
            throw DomainException.Validation("A placa deve conter entre 4 e 10 letras ou números.");
        }

        return normalized;
    }
}
