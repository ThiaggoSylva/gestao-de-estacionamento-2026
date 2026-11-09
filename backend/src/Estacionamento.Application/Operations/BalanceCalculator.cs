using Estacionamento.Domain.Common;
using Estacionamento.Domain.Operations;

namespace Estacionamento.Application.Operations;

public static class BalanceCalculator
{
    public static (decimal Charged, decimal Paid, decimal Outstanding) Calculate(ParkingSession session)
    {
        var charged = Money.Normalize(
            session.Charges.Sum(charge => charge.TotalAmount)
            + session.Adjustments.Sum(adjustment => adjustment.Amount));
        var paid = Money.Normalize(session.Payments.Sum(payment => payment.Amount));
        return (charged, paid, Money.Normalize(charged - paid));
    }
}
