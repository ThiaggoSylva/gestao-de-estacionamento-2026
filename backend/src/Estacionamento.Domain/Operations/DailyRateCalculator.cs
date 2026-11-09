namespace Estacionamento.Domain.Operations;

public static class DailyRateCalculator
{
    public static int CalculateDailyCount(DateTime entryAtUtc, DateTime requestedAtUtc)
    {
        var elapsed = requestedAtUtc - entryAtUtc;
        if (elapsed <= TimeSpan.Zero)
        {
            return 1;
        }

        var startedPeriods = Math.Ceiling(elapsed.Ticks / (double)TimeSpan.TicksPerDay);
        return Math.Max(1, checked((int)startedPeriods));
    }
}
