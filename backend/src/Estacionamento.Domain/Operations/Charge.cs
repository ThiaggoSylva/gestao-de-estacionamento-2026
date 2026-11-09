using Estacionamento.Domain.Common;

namespace Estacionamento.Domain.Operations;

public sealed class Charge
{
    private Charge()
    {
    }

    public Guid Id { get; private set; }
    public Guid ParkingSessionId { get; private set; }
    public Guid WithdrawalRequestId { get; private set; }
    public long ElapsedSeconds { get; private set; }
    public int DailyCount { get; private set; }
    public decimal DailyRate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Charge Fix(
        Guid parkingSessionId,
        Guid withdrawalRequestId,
        DateTime entryAtUtc,
        DateTime requestedAtUtc,
        decimal capturedDailyRate,
        DateTime createdAtUtc)
    {
        var elapsed = requestedAtUtc - entryAtUtc;
        var elapsedSeconds = elapsed > TimeSpan.Zero ? (long)elapsed.TotalSeconds : 0;
        var dailyCount = DailyRateCalculator.CalculateDailyCount(entryAtUtc, requestedAtUtc);
        var total = Money.Normalize(dailyCount * capturedDailyRate);

        return new Charge
        {
            Id = Guid.NewGuid(),
            ParkingSessionId = parkingSessionId,
            WithdrawalRequestId = withdrawalRequestId,
            ElapsedSeconds = elapsedSeconds,
            DailyCount = dailyCount,
            DailyRate = capturedDailyRate,
            TotalAmount = total,
            CreatedAtUtc = createdAtUtc
        };
    }
}
