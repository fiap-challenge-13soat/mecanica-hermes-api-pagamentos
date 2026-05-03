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
- Cross-service: contrato `Mecanica.Hermes.Application.OrdensDeServico.Messages.Requests.PagamentoAprovadoRequest` com **namespace alinhado ao projeto da OS** para roteamento MassTransit por `MessageUrn`.
