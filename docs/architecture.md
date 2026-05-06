# Arquitetura

**Clean Architecture + DDD + CQRS** com a mesma divisão da API de Ordem de Serviço:

```
Api ─────► Application ─────► Domain
            ▲
Infrastructure ────────────────┘ (implementa ports)
```

## Camadas

| Projeto | Responsabilidade |
| --- | --- |
| `Domain` | Agregado `Pagamento`, State Pattern, value objects (`Dinheiro`, `EmailCliente`, `LinkPagamento`), domain events, `Result<T>`. |
| `Application` | Commands/Queries (MediatR), Consumers MassTransit, Ports (`IPagamentoRepository`, `IMercadoPagoClient`, `IPagamentoMetrics`, `IWebhookEventRepository`, `IPagamentoSchedulerOptions`), DTOs, mapeamentos. |
| `Infrastructure` | Driver Mongo, repositórios, **Outbox em MongoDB**, **SAGA + DelayedMessageScheduler** (MassTransit + RabbitMQ), Mercado Pago (SDK + Polly), Observability (OpenTelemetry + Serilog), DI. |
| `Api` | Minimal API, middlewares, presenter, validators, endpoints. |

## Padrões aplicados

- **State Pattern** — cada status do Pagamento é uma classe que conhece suas próprias transições válidas.
- **Result Pattern** — operações retornam `Result` ou `Result<T>` carregando `HttpStatusCode` + `Errors`.
- **CQRS** — escrita assíncrona via `AsyncCommandHandler` → MassTransit → Consumer; leitura síncrona via Query Handlers.
- **Outbox transacional em MongoDB** — eventos persistidos junto do agregado e despachados pelo `MongoOutboxProcessor` com claim atômico.
- **SAGA serializadora** — uma operação em voo por `PagamentoId` (timeout sentinel via `DelayedMessageScheduler`).

## Decisões em relação à OS

- Persistência **MongoDB** (sem EF Core, sem migrations relacionais) — driver oficial 3.x + collection-level optimistic locking via campo `Version`.
- **Outbox em Mongo** — claim com `findOneAndUpdate` (atomicidade do nível single-document) + TTL para reaquisição.
- Polling agendado via `IMessageScheduler` do MassTransit (`UseDelayedMessageScheduler` + plugin `rabbitmq_delayed_message_exchange`).
- Cross-service: contratos decorados com `[EntityName("kebab-case")]` — roteamento por `EntityName`, não por `MessageUrn` derivado do namespace. Os contratos legados (`PagamentoAprovadoRequest` com namespace OS, `EnviarEmailPagamentoCommand`, `PagamentoRecusadoIntegrationEvent`) foram substituídos por `PagamentoConfirmadoEvent`, `PagamentoRecusadoEvent` e `LinkPagamentoGeradoEvent`.

## HTTP Client M2M — Cadastros API

Para criar um pagamento iniciado pelo evento cross-service `OrdemDeServicoAguardandoPagamentoEvent`, a API de Pagamentos precisa consultar o e-mail e o nome do cliente na API de Cadastros.

| Componente | Localização | Descrição |
| --- | --- | --- |
| `IClienteApiClient` | `Application/Pagamentos/Ports/` | Port (interface) para consulta de clientes |
| `ClienteApiClient` | `Infrastructure/HttpClients/` | Implementação HTTP com Polly resilience via `AddStandardResilienceHandler` |
| `ClienteDto` | `Application/Pagamentos/Dtos/` | POCO com `Id`, `Email`, `NomeCivil`, `NomeSocial?` |

**Configuração:**

```bash
CADASTROS_API__BASE_URL=http://localhost:5000   # URL base da API de Cadastros
AUTH__SERVICO_SCOPE=servico-scope               # Scope JWT para autenticação M2M
```

O `ClienteApiClient` é registrado com `AddHttpClient<IClienteApiClient, ClienteApiClient>()` e resilience configurado com timeout de 10s por tentativa, 2 retries e circuit breaker após 5 falhas. Para o contexto de autenticação M2M, consulte [`docs/configuration.md#cadastros-api-m2m`](./configuration.md#cadastros-api-m2m).
