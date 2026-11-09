using Estacionamento.Application.Common;

namespace Estacionamento.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
