namespace Estacionamento.Application.Common;

public interface IClock
{
    DateTime UtcNow { get; }
}
