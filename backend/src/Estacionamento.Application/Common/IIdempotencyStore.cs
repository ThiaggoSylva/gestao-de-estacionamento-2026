namespace Estacionamento.Application.Common;

public sealed record IdempotencyRecordResult(string RequestHash, string ResponseJson, int StatusCode);

public interface IIdempotencyStore
{
    Task<IdempotencyRecordResult?> FindAsync(
        Guid actorUserId,
        string operation,
        string key,
        CancellationToken cancellationToken);

    Task AddAsync(
        Guid actorUserId,
        string operation,
        string key,
        string requestHash,
        string responseJson,
        int statusCode,
        DateTime createdAtUtc,
        CancellationToken cancellationToken);
}
