namespace Mecanica.Hermes.Pagamento.Infrastructure.Settings;

public static class AuthPolicies
{
    public static readonly string AllowClienteScope =
        Environment.GetEnvironmentVariable(VariableNames.AuthClienteScope) ?? "cliente-scope";

    public static readonly string OnlyAdminScope =
        Environment.GetEnvironmentVariable(VariableNames.AuthAdminScope) ?? "admin-scope";
}
