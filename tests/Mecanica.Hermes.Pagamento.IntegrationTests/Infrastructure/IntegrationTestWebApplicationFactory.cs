using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using Mecanica.Hermes.Shared.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace Mecanica.Hermes.Pagamento.IntegrationTests.Infrastructure;

/// <summary>
///     Factory base dos testes de integração — sobe Mongo (replica set) via Testcontainers,
///     configura env vars de bootstrap, sobe a Web API real em ambiente Testing (com
///     DevelopmentAuthenticationMiddleware ativo, MassTransit InMemory transport e WebhookSecret
///     conhecido). O <see cref="IClienteApiClient" /> é substituído por um fake controlável,
///     evitando dependência de rede para a API de Cadastros nos testes.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = "Instanciado pelo xUnit ICollectionFixture.")]
public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Secret HMAC fixo usado para assinar payloads do webhook nos testes.</summary>
    public const string WebhookSecret = "integration-tests-secret-32-bytes-aaa";

    /// <summary>Token de acesso do MercadoPago (placeholder — não há chamadas reais nos testes).</summary>
    public const string MercadoPagoAccessToken = "TEST-ACCESS-TOKEN";

    // Sem replica set: o standalone basta para os testes atuais. Replica set será
    // necessário quando implementarmos transactional outbox real (sessão Mongo
    // compartilhada entre repositório e UoW). `WithReplicaSet()` no Windows tem
    // problema conhecido de hostname-resolution (driver descobre primary via
    // hostname interno do container e falha do host).
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder("mongo:7.0").Build();

    /// <summary>Fake do <see cref="IClienteApiClient" /> — pré-configurado para devolver um cliente válido.</summary>
    public FakeClienteApiClient ClienteApi { get; } = new();

    public string MongoConnectionString => _mongoContainer.GetConnectionString();
    public string DatabaseName { get; } = $"pagamentos_test_{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();

        // env vars precisam ser setadas ANTES do CreateClient (que dispara o pipeline de DI).
        Environment.SetEnvironmentVariable(VariableNames.MongoDbConnectionString, MongoConnectionString);
        Environment.SetEnvironmentVariable(VariableNames.MongoDbDatabase, DatabaseName);
        Environment.SetEnvironmentVariable(VariableNames.MongoDbSagaDatabase, DatabaseName + "_saga");
        Environment.SetEnvironmentVariable(VariableNames.MercadoPagoWebhookSecret, WebhookSecret);
        Environment.SetEnvironmentVariable(VariableNames.MercadoPagoAccessToken, MercadoPagoAccessToken);
        // Polling do Outbox encurta em testes para que MongoOutboxProcessor despache rápido.
        Environment.SetEnvironmentVariable(VariableNames.OutboxPollInterval, "00:00:00.200");

        // Triggers DI build + hosted services (incluindo MongoIndexInitializer).
        _ = CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentNames.Testing);

        builder.ConfigureTestServices(services =>
        {
            // Substitui o IClienteApiClient por fake controlável (a default registration
            // é via AddHttpClient<IClienteApiClient, ClienteApiClient> — RemoveAll cobre os dois).
            services.RemoveAll<IClienteApiClient>();
            services.AddSingleton<IClienteApiClient>(ClienteApi);
        });
    }

    /// <summary>Helper para limpar coleções entre testes (fixture compartilhada).</summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

        await ctx.Database.GetCollection<PagamentoDocument>(MongoOptions.PagamentosCollection)
            .DeleteManyAsync(FilterDefinition<PagamentoDocument>.Empty);
        await ctx.Database.GetCollection<OutboxDocument>(MongoOptions.OutboxCollection)
            .DeleteManyAsync(FilterDefinition<OutboxDocument>.Empty);
        await ctx.Database.GetCollection<WebhookEventDocument>(MongoOptions.WebhookEventsCollection)
            .DeleteManyAsync(FilterDefinition<WebhookEventDocument>.Empty);

        ClienteApi.Reset();
    }

    public override async ValueTask DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}

/// <summary>
///     Fake mínimo do <see cref="IClienteApiClient" /> — devolve um cliente padrão por default
///     e permite ao teste sobrescrever o resultado por id.
/// </summary>
public sealed class FakeClienteApiClient : IClienteApiClient
{
    private readonly Dictionary<Guid, ClienteDto?> _overrides = new();

    public Task<ClienteDto?> GetByIdAsync(Guid clienteId, CancellationToken ct = default)
    {
        if (_overrides.TryGetValue(clienteId, out var dto))
            return Task.FromResult(dto);

        return Task.FromResult<ClienteDto?>(new ClienteDto(
            clienteId,
            $"cliente-{clienteId:N}@hermes.test",
            NomeCivil: "Cliente Teste de Integração",
            NomeSocial: null));
    }

    public void OverrideClient(Guid id, ClienteDto? dto) => _overrides[id] = dto;

    public void Reset() => _overrides.Clear();
}
