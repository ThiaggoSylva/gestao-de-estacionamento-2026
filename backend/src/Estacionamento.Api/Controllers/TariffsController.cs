using Estacionamento.Application.Operations;
using Estacionamento.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estacionamento.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/tariffs")]
public sealed class TariffsController(AdministrationService administration) : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<TariffDto?>> GetCurrent(CancellationToken cancellationToken)
        => Ok(await administration.GetCurrentTariffAsync(cancellationToken));

    [HttpPost]
    [Authorize(Roles = EmployeeRoles.Administrator)]
    public async Task<ActionResult<TariffDto>> Set(
        SetTariffRequest request,
        CancellationToken cancellationToken)
    {
        var tariff = await administration.SetTariffAsync(
            new SetTariffCommand(request.DailyRate),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, tariff);
    }
}

public sealed record SetTariffRequest(decimal DailyRate);
