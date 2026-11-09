using Estacionamento.Application.Common;
using Estacionamento.Domain.Common;
using Estacionamento.Domain.Operations;

namespace Estacionamento.Application.Operations;

public sealed class AdministrationService(
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork,
    IParkingSpotRepository parkingSpots,
    ITariffRepository tariffs,
    IAuditRepository audits,
    IOperationsQueries queries)
{
    public async Task<SpotDto> CreateSpotAsync(CreateSpotCommand command, CancellationToken cancellationToken)
    {
        AccessGuard.RequireAdministrator(currentUser);

        var spot = ParkingSpot.Create(command.Code);
        if (await parkingSpots.CodeExistsAsync(spot.Code, cancellationToken))
        {
            throw DomainException.Conflict($"A vaga '{spot.Code}' já existe.");
        }

        return await unitOfWork.ExecuteAsync(async token =>
        {
            await parkingSpots.AddAsync(spot, token);
            await audits.AddAsync(
                AuditEntry.Create(null, currentUser.UserId, "SpotCreated", $"Vaga {spot.Code}", clock.UtcNow),
                token);
            return new SpotDto(spot.Id, spot.Code, spot.IsActive, false, null, null);
        }, cancellationToken);
    }

    public async Task<TariffDto> SetTariffAsync(SetTariffCommand command, CancellationToken cancellationToken)
    {
        AccessGuard.RequireAdministrator(currentUser);

        var now = clock.UtcNow;
        var tariff = Tariff.Create(command.DailyRate, now, now, currentUser.UserId);
        return await unitOfWork.ExecuteAsync(async token =>
        {
            await tariffs.AddAsync(tariff, token);
            await audits.AddAsync(
                AuditEntry.Create(null, currentUser.UserId, "TariffCreated",
                    $"Tarifa {tariff.DailyRate:0.00}", now),
                token);
            return new TariffDto(tariff.Id, tariff.DailyRate, tariff.EffectiveFromUtc);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<SpotDto>> GetSpotsAsync(CancellationToken cancellationToken)
    {
        AccessGuard.RequireAuthenticatedRole(currentUser);
        return queries.GetSpotsAsync(cancellationToken);
    }

    public Task<TariffDto?> GetCurrentTariffAsync(CancellationToken cancellationToken)
    {
        AccessGuard.RequireAuthenticatedRole(currentUser);
        return queries.GetCurrentTariffAsync(cancellationToken);
    }
}
