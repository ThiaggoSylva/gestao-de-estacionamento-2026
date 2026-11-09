using Estacionamento.Application.Common;
using Estacionamento.Domain.Common;
using Estacionamento.Domain.Identity;

namespace Estacionamento.Application.Operations;

internal static class AccessGuard
{
    public static void RequireAdministrator(ICurrentUser currentUser)
    {
        if (!currentUser.IsInRole(EmployeeRoles.Administrator))
        {
            throw DomainException.Forbidden("A operação exige perfil de administrador.");
        }
    }

    public static void RequireCashierOrAdministrator(ICurrentUser currentUser)
    {
        if (!currentUser.IsInRole(EmployeeRoles.Cashier)
            && !currentUser.IsInRole(EmployeeRoles.Administrator))
        {
            throw DomainException.Forbidden("A operação exige perfil de caixa ou administrador.");
        }
    }

    public static void RequireAuthenticatedRole(ICurrentUser currentUser)
    {
        if (currentUser.UserId == Guid.Empty)
        {
            throw DomainException.Forbidden("A operação exige sessão autenticada.");
        }
    }
}
