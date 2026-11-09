using System.Security.Claims;
using Estacionamento.Application.Common;

namespace Estacionamento.Api.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
        }
    }

    public string UserName => Principal?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public IReadOnlyCollection<string> Roles
        => Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
