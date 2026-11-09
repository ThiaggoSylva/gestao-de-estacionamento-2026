using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Estacionamento.Domain.Common;

namespace Estacionamento.Application.Common;

public sealed class IdempotentOperationExecutor(
    IUnitOfWork unitOfWork,
    IIdempotencyStore idempotencyStore,
    ICurrentUser currentUser,
    IClock clock)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<T> ExecuteAsync<T>(
        string operation,
        string idempotencyKey,
        object request,
        int successStatusCode,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var actorUserId = currentUser.UserId;
        if (actorUserId == Guid.Empty)
        {
            throw DomainException.Forbidden("A operação exige sessão autenticada.");
        }

        var key = NormalizeKey(idempotencyKey);
        var requestHash = Hash(request);

        var existing = await idempotencyStore.FindAsync(actorUserId, operation, key, cancellationToken);
        if (existing is not null)
        {
            return Replay<T>(existing, requestHash);
        }

        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var replayed = await idempotencyStore.FindAsync(actorUserId, operation, key, token);
                if (replayed is not null)
                {
                    return Replay<T>(replayed, requestHash);
                }

                var result = await action(token);
                await idempotencyStore.AddAsync(
                    actorUserId,
                    operation,
                    key,
                    requestHash,
                    JsonSerializer.Serialize(result, SerializerOptions),
                    successStatusCode,
                    clock.UtcNow,
                    token);
                return result;
            }, cancellationToken);
        }
        catch (DomainException exception) when (exception.Kind == DomainErrorKind.Conflict)
        {
            var retry = await idempotencyStore.FindAsync(actorUserId, operation, key, cancellationToken);
            if (retry is not null && string.Equals(retry.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return Deserialize<T>(retry.ResponseJson);
            }

            throw;
        }
    }

    private static T Replay<T>(IdempotencyRecordResult record, string requestHash)
    {
        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw DomainException.Conflict(
                "A chave de idempotência foi reutilizada com conteúdo diferente.");
        }

        return Deserialize<T>(record.ResponseJson);
    }

    private static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, SerializerOptions)
            ?? throw new InvalidOperationException("O resultado idempotente persistido não pôde ser lido.");

    private static string NormalizeKey(string idempotencyKey)
    {
        var key = (idempotencyKey ?? string.Empty).Trim();
        if (key.Length is < 1 or > 100)
        {
            throw DomainException.Validation("A chave de idempotência deve conter entre 1 e 100 caracteres.");
        }

        return key;
    }

    private static string Hash(object request)
    {
        var json = JsonSerializer.Serialize(request, SerializerOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
