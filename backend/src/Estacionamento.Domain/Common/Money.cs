namespace Estacionamento.Domain.Common;

public static class Money
{
    public const int Scale = 2;

    public static decimal Normalize(decimal value)
        => decimal.Round(value, Scale, MidpointRounding.AwayFromZero);

    public static bool HasSupportedScale(decimal value)
        => decimal.Round(value, Scale) == value;
}
