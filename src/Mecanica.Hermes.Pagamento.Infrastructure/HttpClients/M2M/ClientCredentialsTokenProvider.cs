using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mecanica.Hermes.Pagamento.Infrastructure.HttpClients.M2M;

/// <summary>
///     Implementação OAuth 2.0 Client Credentials do <see cref="IM2MTokenProvider" /> com
///     cache em memória single-instance e renovação automática.
///     <list type="bullet">
///         <item>
///             POSTa <c>grant_type=client_credentials</c> + <c>client_id</c>/<c>client_secret</c>
///             no <see cref="M2MTokenProviderOptions.TokenEndpoint" />.
///         </item>
///         <item>
///             Cacheia o <c>access_token</c> até <c>expires_in - ExpirationSafetyMargin</c>;
///             próxima chamada após esse instante força refresh.
///         </item>
///         <item>
///             Concorrência: <see cref="SemaphoreSlim" /> garante que apenas uma chamada
///             refresha o token enquanto outras esperam.
///         </item>
///     </list>
/// </summary>
internal sealed class ClientCredentialsTokenProvider(
    HttpClient httpClient,
    IOptions<M2MTokenProviderOptions> options,
    TimeProvider timeProvider,
    ILogger<ClientCredentialsTokenProvider> logger) : IM2MTokenProvider, IDisposable
{
    private readonly M2MTokenProviderOptions _options = options.Value;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _cachedTokenExpiresAt = DateTimeOffset.MinValue;

    public void Dispose()
    {
        _refreshLock.Dispose();
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!_options.IsEnabled)
            throw new InvalidOperationException(
                "M2MTokenProvider chamado sem configuração válida (M2M__TOKEN_ENDPOINT/CLIENT_ID/CLIENT_SECRET vazios). " +
                "Em Development/Testing o handler M2M não deve ser encadeado.");

        var now = timeProvider.GetUtcNow();
        if (_cachedToken is not null && _cachedTokenExpiresAt > now)
            return _cachedToken;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check: outra thread pode ter renovado enquanto esperávamos o semáforo.
            now = timeProvider.GetUtcNow();
            if (_cachedToken is not null && _cachedTokenExpiresAt > now)
                return _cachedToken;

            var token = await FetchTokenAsync(cancellationToken);
            _cachedToken = token.AccessToken;
            _cachedTokenExpiresAt = now
                .AddSeconds(token.ExpiresIn)
                .Subtract(_options.ExpirationSafetyMargin);

            logger.LogInformation(
                "M2M token renovado. Próxima renovação prevista em {ExpiresAt:O} (expires_in={ExpiresIn}s).",
                _cachedTokenExpiresAt, token.ExpiresIn);

            return _cachedToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<TokenResponse> FetchTokenAsync(CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = _options.Scope
            })
        };

        // Basic auth com client_id:client_secret é o formato preferido por Cognito;
        // alguns IDPs aceitam também os campos no body — usamos Basic por padrão.
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError(
                "Falha ao obter M2M token. Status={Status}, Body={Body}",
                (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException(
                "Token endpoint retornou sucesso mas access_token está vazio.");

        return token;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")]
        string AccessToken,
        [property: JsonPropertyName("expires_in")]
        int ExpiresIn,
        [property: JsonPropertyName("token_type")]
        string? TokenType);
}
