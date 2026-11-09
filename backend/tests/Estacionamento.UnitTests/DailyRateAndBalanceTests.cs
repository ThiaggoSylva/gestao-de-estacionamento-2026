using Estacionamento.Application.Operations;
using Estacionamento.Domain.Common;
using Estacionamento.Domain.Operations;

namespace Estacionamento.UnitTests;

[TestClass]
public sealed class DailyRateAndBalanceTests
{
    private static readonly DateTime Entry = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Actor = Guid.NewGuid();

    [TestMethod]
    public void Daily_count_charges_each_started_period_with_a_minimum_of_one()
    {
        Assert.AreEqual(1, DailyRateCalculator.CalculateDailyCount(Entry, Entry));
        Assert.AreEqual(1, DailyRateCalculator.CalculateDailyCount(Entry, Entry.AddMinutes(30)));
        Assert.AreEqual(1, DailyRateCalculator.CalculateDailyCount(Entry, Entry.AddHours(24)));
        Assert.AreEqual(2, DailyRateCalculator.CalculateDailyCount(Entry, Entry.AddHours(24).AddSeconds(1)));
        Assert.AreEqual(3, DailyRateCalculator.CalculateDailyCount(Entry, Entry.AddHours(49)));
        Assert.AreEqual(1, DailyRateCalculator.CalculateDailyCount(Entry, Entry.AddHours(-2)));
    }

    [TestMethod]
    public void Balance_uses_charges_adjustments_and_payments_with_currency_rounding()
    {
        var session = NewSession();
        session.Charges.Add(Charge.Fix(
            session.Id,
            Guid.NewGuid(),
            Entry,
            Entry.AddHours(30),
            session.CapturedDailyRate,
            Entry.AddHours(30)));
        session.Adjustments.Add(Adjustment.Create(
            session.Id,
            -5.55m,
            "Correção administrativa manual",
            Entry.AddHours(31),
            Actor));
        session.Payments.Add(Payment.Create(
            session.Id,
            60.00m,
            PaymentMethod.Cash,
            Entry.AddHours(32),
            Actor));

        var (charged, paid, outstanding) = BalanceCalculator.Calculate(session);

        Assert.AreEqual(64.45m, charged);
        Assert.AreEqual(60.00m, paid);
        Assert.AreEqual(4.45m, outstanding);
    }

    [TestMethod]
    public void Balance_can_become_credit_when_adjustment_exceeds_charge()
    {
        var session = NewSession();
        session.Charges.Add(Charge.Fix(
            session.Id,
            Guid.NewGuid(),
            Entry,
            Entry,
            session.CapturedDailyRate,
            Entry));
        session.Adjustments.Add(Adjustment.Create(
            session.Id,
            -45.00m,
            "Desconto administrativo aprovado",
            Entry,
            Actor));

        var (charged, paid, outstanding) = BalanceCalculator.Calculate(session);

        Assert.AreEqual(-10.00m, charged);
        Assert.AreEqual(0.00m, paid);
        Assert.AreEqual(-10.00m, outstanding);
    }

    [TestMethod]
    public void Charge_preserves_the_captured_daily_rate_snapshot()
    {
        var session = NewSession(dailyRate: 42.10m);
        var requestedAt = Entry.AddHours(25);
        var charge = Charge.Fix(session.Id, Guid.NewGuid(), Entry, requestedAt, session.CapturedDailyRate, requestedAt);

        Assert.AreEqual(2, charge.DailyCount);
        Assert.AreEqual(42.10m, charge.DailyRate);
        Assert.AreEqual(84.20m, charge.TotalAmount);
        Assert.AreEqual((long)TimeSpan.FromHours(25).TotalSeconds, charge.ElapsedSeconds);
        Assert.AreEqual(session.CapturedDailyRate, charge.DailyRate);
    }

    private static ParkingSession NewSession(decimal dailyRate = 35.00m)
        => ParkingSession.Create(
            "PUB-1",
            Guid.NewGuid(),
            "abc-1d23",
            "Sedan",
            "Maria",
            "protected-document",
            Entry,
            Actor,
            dailyRate);
}
