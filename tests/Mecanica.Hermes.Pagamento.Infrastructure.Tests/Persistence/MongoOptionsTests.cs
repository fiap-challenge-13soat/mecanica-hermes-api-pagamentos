using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.Persistence;

public class MongoOptionsTests
{
    [Fact]
    public void Constants_HaveExpectedValues()
    {
        MongoOptions.DefaultDatabase.Should().Be("mechermes_pagamento");
        MongoOptions.DefaultSagaDatabase.Should().Be("mechermes_pagamento_saga");
        MongoOptions.PagamentosCollection.Should().Be("pagamentos");
        MongoOptions.OutboxCollection.Should().Be("pagamento_outbox");
        MongoOptions.WebhookEventsCollection.Should().Be("webhook_events");
    }

    [Fact]
    public void Defaults_DatabaseNamesUseConstants()
    {
        var opts = new MongoOptions();

        opts.ConnectionString.Should().BeEmpty();
        opts.DatabaseName.Should().Be(MongoOptions.DefaultDatabase);
        opts.SagaDatabaseName.Should().Be(MongoOptions.DefaultSagaDatabase);
    }

    [Fact]
    public void Setters_PersistValues()
    {
        var opts = new MongoOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "custom_db",
            SagaDatabaseName = "custom_saga"
        };

        opts.ConnectionString.Should().Be("mongodb://localhost:27017");
        opts.DatabaseName.Should().Be("custom_db");
        opts.SagaDatabaseName.Should().Be("custom_saga");
    }
}
