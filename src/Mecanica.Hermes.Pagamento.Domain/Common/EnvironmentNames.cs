namespace Mecanica.Hermes.Pagamento.Domain.Common;

/// <summary>
///     Nomes de ambientes utilizados pela aplicação (alinhados com a convenção
///     <c>ASPNETCORE_ENVIRONMENT</c>). Centralizados no Domain para que qualquer
///     camada possa referenciá-los sem dependência circular.
/// </summary>
public static class EnvironmentNames
{
    public const string Development = "Development";
    public const string Production = "Production";

    /// <summary>
    ///     Nome do ambiente usado pelos testes de integração / E2E. Quando ativo, autenticação
    ///     e logging Serilog são substituídos por adapters de teste.
    /// </summary>
    public const string Testing = "Testing";
}
