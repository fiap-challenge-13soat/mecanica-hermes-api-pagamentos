namespace Mecanica.Hermes.Pagamento.Infrastructure.HttpClients.M2M;

/// <summary>
///     Configuração do <see cref="ClientCredentialsTokenProvider" />, lida das env vars
///     <c>M2M__*</c>. Quando <see cref="TokenEndpoint" /> está vazio, o
///     <see cref="M2MAuthorizationDelegatingHandler" /> não é encadeado — ideal para
///     Development/Testing onde a API de Cadastros bypassa auth via
///     <c>DevelopmentAuthenticationMiddleware</c>.
/// </summary>
internal sealed class M2MTokenProviderOptions
{
    /// <summary>
    ///     OAuth 2.0 token endpoint do IDP (ex.: Cognito User Pool ou IDP genérico):
    ///     <c>https://{userpool}.auth.{region}.amazoncognito.com/oauth2/token</c>.
    ///     Vazio em Development/Testing — desliga o handler.
    /// </summary>
    public string TokenEndpoint { get; set; } = string.Empty;

    /// <summary>Client ID da app M2M registrada no IDP.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client Secret. Em produção, vir de Vault/Secrets Manager — nunca commitado.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    ///     Scope solicitado no token (ex.: <c>"servico-scope"</c> conforme ADR-006 de Cadastros).
    ///     Mapeia para a policy <c>AllowServicoOrAdminScope</c> em Cadastros.
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    ///     Margem aplicada ao <c>expires_in</c> retornado pelo IDP — o token em cache é descartado
    ///     este valor antes da expiração real para evitar usar um JWT prestes a expirar enquanto
    ///     uma request está em voo. Default: 60s.
    /// </summary>
    public TimeSpan ExpirationSafetyMargin { get; set; } = TimeSpan.FromSeconds(60);

    public bool IsEnabled => !string.IsNullOrWhiteSpace(TokenEndpoint)
                             && !string.IsNullOrWhiteSpace(ClientId)
                             && !string.IsNullOrWhiteSpace(ClientSecret);
}
