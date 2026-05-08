using System.Net.Http.Headers;

namespace Mecanica.Hermes.Pagamento.Infrastructure.HttpClients.M2M;

/// <summary>
///     <see cref="DelegatingHandler" /> que injeta um <c>Authorization: Bearer &lt;jwt&gt;</c>
///     em cada outgoing request usando o <see cref="IM2MTokenProvider" />.
///     Encadeado <em>antes</em> do <c>StandardResilienceHandler</c> para que, em caso de
///     401 transiente (token expirou enquanto estava em cache), o retry use um token novo.
/// </summary>
internal sealed class M2MAuthorizationDelegatingHandler(IM2MTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Não sobrescreve token se a request já tem Authorization (ex.: chamada autenticada
        // que propaga claims do usuário — não é o caso atual, mas blinda o handler).
        if (request.Headers.Authorization is null)
        {
            var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
