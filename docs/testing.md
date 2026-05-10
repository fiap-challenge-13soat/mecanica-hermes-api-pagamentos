# Testes

| Projeto | Estratégia | Dependências |
| --- | --- | --- |
| `Domain.Tests` | Unit puro, sem mocks (domínio é side-effect free). | xUnit, FluentAssertions, Bogus |
| `Application.Tests` | Consumers via `MassTransit.TestFramework` (`InMemoryTestHarness`). Handlers MediatR com Moq. | Moq, MassTransit.TestFramework |
| `Infrastructure.Tests` | Repositórios contra Testcontainers MongoDB; Mercado Pago via WireMock.Net; SAGA via `InMemoryTestHarness`. | Testcontainers.MongoDb, WireMock.Net |
| `Api.Tests` | Middleware e presenter como unit. | Moq |
| `IntegrationTests` | `WebApplicationFactory` + Testcontainers (Mongo + RabbitMQ) + WireMock (MP). | Microsoft.AspNetCore.Mvc.Testing |
| `ArchitectureTests` | NetArchTest — regras de dependência. | NetArchTest.Rules |
| `TestUtils` | `PagamentoBuilder` (Bogus) e factories compartilhadas. | Bogus |

## Cobertura

CI exige **≥ 80%** de cobertura de linhas (medida por coverlet via `coverlet.runsettings`,
report consolidado por `reportgenerator`). Excluem-se: `Program.cs`, `*Tests`, `TestUtils`,
classes `[ExcludeFromCodeCoverage]`.

## Convenções para consumers

Use o `PagamentoBuilder.BuildAguardandoConfirmacao()` quando precisar de um pagamento já com
link gerado e em estado válido para confirmar/recusar — evita duplicar a sequência de transições
em vários testes.

Mocks devem retornar pagamentos no estado esperado pelo consumer. Para
`ConfirmarPagamentoConsumer`, configure `IPagamentoRepository.GetByIdAsync(id)` para retornar
um pagamento `AguardandoConfirmacao` e verifique:

1. `PagamentoConfirmadoEvent` foi publicado;
2. `PagamentoOperationCompleted` foi publicado;
3. `IPagamentoMetrics.PagamentoConfirmado` foi chamado.
