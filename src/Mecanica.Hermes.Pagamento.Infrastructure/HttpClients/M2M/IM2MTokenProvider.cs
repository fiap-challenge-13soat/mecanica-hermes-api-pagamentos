namespace Mecanica.Hermes.Pagamento.Infrastructure.HttpClients.M2M;

/// <summary>
///     Provê tokens JWT para autenticação machine-to-machine. A implementação
///     <see cref="ClientCredentialsTokenProvider" /> usa OAuth 2.0 Client Credentials
///     com cache em memória (renova quando <c>expires_in</c> está prestes a passar).
/// </summary>
internal interface IM2MTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
