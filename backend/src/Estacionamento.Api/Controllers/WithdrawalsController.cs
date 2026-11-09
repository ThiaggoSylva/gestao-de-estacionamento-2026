using Estacionamento.Application.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estacionamento.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/withdrawals")]
public sealed class WithdrawalsController(WithdrawalService withdrawal) : ControllerBase
{
    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<QueueItemDto>>> GetQueue(CancellationToken cancellationToken)
        => Ok(await withdrawal.GetQueueAsync(cancellationToken));

    [HttpPost("{id:guid}/search")]
    public async Task<ActionResult<WithdrawalStateDto>> StartSearch(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await withdrawal.StartSearchAsync(new StartSearchCommand(id), cancellationToken));
}
