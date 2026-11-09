using Estacionamento.Application.Operations;
using Estacionamento.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estacionamento.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/spots")]
public sealed class SpotsController(AdministrationService administration) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SpotDto>>> List(CancellationToken cancellationToken)
        => Ok(await administration.GetSpotsAsync(cancellationToken));

    [HttpPost]
    [Authorize(Roles = EmployeeRoles.Administrator)]
    public async Task<ActionResult<SpotDto>> Create(
        CreateSpotRequest request,
        CancellationToken cancellationToken)
    {
        var spot = await administration.CreateSpotAsync(
            new CreateSpotCommand(request.Code),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, spot);
    }
}

public sealed record CreateSpotRequest(string Code);
