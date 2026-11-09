using Estacionamento.Application.Common;
using Estacionamento.Domain.Common;
using Estacionamento.Infrastructure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Estacionamento.Infrastructure.Persistence;

public sealed class EfUnitOfWork(IdentityDbContext database) : IUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DomainException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw DomainException.Conflict(
                "O registro foi alterado por outra operação. Recarregue o estado e tente novamente.");
        }
        catch (DbUpdateException exception) when (FindSqlException(exception) is { } sqlException)
        {
            await transaction.RollbackAsync(cancellationToken);
            if (sqlException.Number is 2601 or 2627)
            {
                throw DomainException.Conflict(
                    "A operação conflita com um registro existente. Recarregue o estado e tente novamente.");
            }

            if (sqlException.Number == 547)
            {
                throw DomainException.RuleViolation(
                    "A operação viola uma referência obrigatória do atendimento.");
            }

            throw;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException)
            {
                return sqlException;
            }
        }

        return null;
    }
}
