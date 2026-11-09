namespace Estacionamento.Domain.Identity;

public static class EmployeeRoles
{
    public const string Valet = "manobrista";
    public const string Cashier = "caixa";
    public const string Administrator = "admin";
    public const string CashierOrAdministrator = Cashier + "," + Administrator;

    public static readonly IReadOnlyList<string> All = [Valet, Cashier, Administrator];
}
