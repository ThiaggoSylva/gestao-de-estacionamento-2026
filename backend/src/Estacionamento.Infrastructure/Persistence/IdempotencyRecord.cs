namespace Estacionamento.Infrastructure.Persistence;

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
