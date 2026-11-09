namespace Estacionamento.Domain.Operations;

public enum ParkingSessionState
{
    Active = 1,
    AwaitingWithdrawal = 2,
    Searching = 3,
    Delivered = 4
}
