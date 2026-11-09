using Estacionamento.Application.Common;
using Estacionamento.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Estacionamento.Infrastructure.Persistence;

public sealed class EfIdempotencyStore(IdentityDbContext database) : IIdempotencyStore
{
    public async Task<IdempotencyRecordResult?> FindAsync(
        Guid actorUserId,
        string operation,
        string key,
        CancellationToken cancellationToken)
    {
        var record = await database.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.ActorUserId == actorUserId
                    && item.Operation == operation
                    && item.Key == key,
                cancellationToken);

        return record is null
            ? null
            : new IdempotencyRecordResult(record.RequestHash, record.ResponseJson, record.StatusCode);
    }

    public async Task AddAsync(
        Guid actorUserId,
        string operation,
        string key,
        string requestHash,
        string responseJson,
        int statusCode,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        await database.IdempotencyRecords.AddAsync(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Operation = operation,
            Key = key,
            RequestHash = requestHash,
            ResponseJson = responseJson,
            StatusCode = statusCode,
            CreatedAtUtc = createdAtUtc
        }, cancellationToken);
    }
}
