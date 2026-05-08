using System.Net;
using System.Text;
using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.HttpClients.M2M;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.HttpClients.M2M;

[Trait("Category", "Unit")]
public class ClientCredentialsTokenProviderTests
{
    private static M2MTokenProviderOptions ValidOptions(TimeSpan? safetyMargin = null)
    {
        return new M2MTokenProviderOptions
        {
            TokenEndpoint = "https://idp.test/oauth2/token",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Scope = "servico-scope",
            ExpirationSafetyMargin = safetyMargin ?? TimeSpan.FromSeconds(60)
        };
    }

    private static (ClientCredentialsTokenProvider sut, MockHttpMessageHandler handler, FakeTimeProvider time)
        BuildSut(M2MTokenProviderOptions? options = null)
    {
        var opts = options ?? ValidOptions();
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var sut = new ClientCredentialsTokenProvider(
            httpClient,
            Options.Create(opts),
            time,
            NullLogger<ClientCredentialsTokenProvider>.Instance);

        return (sut, handler, time);
    }

    [Fact]
    public async Task GetAccessTokenAsync_FirstCall_FetchesFromEndpoint()
    {
        var (sut, handler, _) = BuildSut();
        handler.Enqueue("""{"access_token":"abc123","expires_in":3600,"token_type":"Bearer"}""");

        var token = await sut.GetAccessTokenAsync(CancellationToken.None);

        token.Should().Be("abc123");
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithinExpiry_ReturnsCachedToken()
    {
        var (sut, handler, _) = BuildSut();
        handler.Enqueue("""{"access_token":"cached","expires_in":3600,"token_type":"Bearer"}""");

        var first = await sut.GetAccessTokenAsync(CancellationToken.None);
        var second = await sut.GetAccessTokenAsync(CancellationToken.None);

        first.Should().Be("cached");
        second.Should().Be("cached");
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAccessTokenAsync_AfterSafetyMargin_FetchesNewToken()
    {
        var (sut, handler, time) = BuildSut(ValidOptions(safetyMargin: TimeSpan.FromSeconds(60)));
        handler.Enqueue("""{"access_token":"first","expires_in":600,"token_type":"Bearer"}""");
        handler.Enqueue("""{"access_token":"second","expires_in":600,"token_type":"Bearer"}""");

        var first = await sut.GetAccessTokenAsync(CancellationToken.None);

        // expires_in=600s; safety margin=60s; cache válido até now+540s.
        // Avança 541s: cache expirado, deve forçar refresh.
        time.Advance(TimeSpan.FromSeconds(541));

        var second = await sut.GetAccessTokenAsync(CancellationToken.None);

        first.Should().Be("first");
        second.Should().Be("second");
        handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAccessTokenAsync_BeforeSafetyMargin_ReusesToken()
    {
        var (sut, handler, time) = BuildSut(ValidOptions(safetyMargin: TimeSpan.FromSeconds(60)));
        handler.Enqueue("""{"access_token":"reused","expires_in":600,"token_type":"Bearer"}""");

        var first = await sut.GetAccessTokenAsync(CancellationToken.None);

        // expires_in=600s; safety margin=60s; cache válido até now+540s.
        // Avança 539s: ainda dentro da margem, reusa cache.
        time.Advance(TimeSpan.FromSeconds(539));

        var second = await sut.GetAccessTokenAsync(CancellationToken.None);

        first.Should().Be("reused");
        second.Should().Be("reused");
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ConcurrentCalls_OnlyOneFetch()
    {
        var (sut, handler, _) = BuildSut();
        // Bloqueia o handler para garantir que múltiplas calls cheguem ao semáforo simultaneamente
        handler.EnqueueWithDelay("""{"access_token":"once","expires_in":3600,"token_type":"Bearer"}""",
            TimeSpan.FromMilliseconds(100));

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.GetAccessTokenAsync(CancellationToken.None))
            .ToArray();

        var tokens = await Task.WhenAll(tasks);

        tokens.Should().AllBe("once");
        handler.RequestCount.Should().Be(1,
            "SemaphoreSlim deve serializar refresh e double-check evita 2ª chamada após 1ª popular o cache");
    }

    [Fact]
    public async Task GetAccessTokenAsync_NotConfigured_Throws()
    {
        var (sut, _, _) = BuildSut(new M2MTokenProviderOptions
        {
            // TokenEndpoint vazio = IsEnabled false
            TokenEndpoint = string.Empty,
            ClientId = "x",
            ClientSecret = "y",
            Scope = "z"
        });

        var act = () => sut.GetAccessTokenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sem configuração válida*");
    }

    [Fact]
    public async Task GetAccessTokenAsync_EndpointReturnsError_Throws()
    {
        var (sut, handler, _) = BuildSut();
        handler.EnqueueError(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_client\"}");

        var act = () => sut.GetAccessTokenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAccessTokenAsync_PostsClientCredentialsGrant_WithBasicAuth()
    {
        var (sut, handler, _) = BuildSut();
        handler.Enqueue("""{"access_token":"x","expires_in":3600,"token_type":"Bearer"}""");

        await sut.GetAccessTokenAsync(CancellationToken.None);

        var captured = handler.LastRequest!;
        captured.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.ToString().Should().Be("https://idp.test/oauth2/token");

        captured.Headers.Authorization!.Scheme.Should().Be("Basic");
        var expectedCreds = Convert.ToBase64String(
            Encoding.UTF8.GetBytes("test-client:test-secret"));
        captured.Headers.Authorization.Parameter.Should().Be(expectedCreds);

        var body = await captured.Content!.ReadAsStringAsync();
        body.Should().Contain("grant_type=client_credentials");
        body.Should().Contain("scope=servico-scope");
    }

    // ----------------------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------------------

    /// <summary>
    ///     <see cref="TimeProvider" /> controlável — avança quando explicitado pelo teste.
    ///     Substitui <c>Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider</c> sem o package.
    /// </summary>
    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }

        public void Advance(TimeSpan delta)
        {
            _now = _now.Add(delta);
        }
    }

    /// <summary>
    ///     <see cref="HttpMessageHandler" /> mock para testar <see cref="HttpClient" /> sem rede.
    ///     Suporta fila de respostas, captura da última request e atrasos artificiais para testar
    ///     concorrência.
    /// </summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpResponseMessage response, TimeSpan delay)> _responses = new();

        public int RequestCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        public void Enqueue(string jsonBody, HttpStatusCode status = HttpStatusCode.OK)
        {
            _responses.Enqueue((new HttpResponseMessage(status)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            }, TimeSpan.Zero));
        }

        public void EnqueueWithDelay(string jsonBody, TimeSpan delay)
        {
            _responses.Enqueue((new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            }, delay));
        }

        public void EnqueueError(HttpStatusCode status, string body = "")
        {
            _responses.Enqueue((new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }, TimeSpan.Zero));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            // Buffer the body so the test can read it after the call returns
            if (request.Content is not null)
                await request.Content.LoadIntoBufferAsync(cancellationToken);
            LastRequest = request;

            if (_responses.Count == 0)
                throw new InvalidOperationException("Nenhuma resposta enfileirada para esta request.");

            var (response, delay) = _responses.Dequeue();
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);
            return response;
        }
    }
}
