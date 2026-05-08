# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Ecosystem Context

This repository is **one of five** in the Mecânica Hermes ecosystem. They live as siblings under
`C:\git\mecanica-hermes\` (or equivalent). When working here, keep in mind:

| Repo | Role | Key tech |
|---|---|---|
| `mecanica-hermes-api-pagamentos` | **This repo** — integração Mercado Pago, gera link, recebe webhook MP, polling de status | .NET 10, MongoDB |
| `mecanica-hermes-api-ordem-servico` | Orquestra ordens de serviço (state machine, SAGA principal) | .NET 10, PostgreSQL, RabbitMQ |
| `mecanica-hermes-cadastros` | Cadastros de Clientes e Produtos; recebe webhook de aprovação/rejeição de orçamento | .NET 10, PostgreSQL |
| `mecanica-hermes-tests-e2e` | Suíte E2E Robot Framework BDD cobrindo as 3 APIs em Docker Compose | Python 3.11, Robot Framework, Allure |
| `mecanica-hermes-shared-sdk` | SDK compartilhado em GitHub Packages (6 pacotes NuGet) | .NET 10, MassTransit.Abstractions |

### Cross-service integration

- **Mensageria** (RabbitMQ via MassTransit): 4 eventos versionados via `[EntityName(".v1")]` que
  esta API publica/consome (ver "Cross-service Integration Events" abaixo). O sufixo `.v1` é
  contrato — qualquer mudança de schema cria `.v2` em paralelo (deprecation window).
- **HTTP M2M**: esta API consome `GET /api/clientes/{id}` do Cadastros para obter dados do cliente
  ao gerar link Mercado Pago (autenticação via JWT M2M; no Development o
  `DevelopmentAuthenticationMiddleware` bypassa).
- **Webhook signed**: o endpoint `POST /api/webhooks/mercadopago` é público mas validado por HMAC
  via `MERCADO_PAGO__WEBHOOK_SECRET`.

### Shared SDK status (em adoção)

PR #1 do `mecanica-hermes-shared-sdk` ainda **não foi mergeado** quando este CLAUDE.md foi escrito.
Quando for: 6 pacotes NuGet (`Mecanica.Hermes.Shared.Core`, `.Application`, `.AspNetCore`,
`.Observability`, `.MassTransit`, `Mecanica.Hermes.Contracts`) substituirão ~700 linhas duplicadas
entre os 3 repos de API. Até lá, **não mover código para o SDK** sem alinhar com o usuário primeiro.

## Commands

```bash
# Build
dotnet build --configuration Release

# Run API locally
dotnet run --project src/Mecanica.Hermes.Pagamento.Api

# Run all tests
dotnet test Mecanica.Hermes.Pagamento.sln

# Run only unit tests (no Docker required)
dotnet test tests/Mecanica.Hermes.Pagamento.Domain.Tests
dotnet test tests/Mecanica.Hermes.Pagamento.Application.Tests

# Run tests with coverage
dotnet test Mecanica.Hermes.Pagamento.sln --settings coverlet.runsettings --results-directory .temp/coverage

# Generate coverage report
reportgenerator -reports:.temp/coverage/**/coverage.opencover.xml -targetdir:.temp/coverage/report -reporttypes:TextSummary;Html
```

Integration tests (`Mecanica.Hermes.Pagamento.IntegrationTests`) require Docker to spin up a MongoDB
container (replica set, for transactions) and a RabbitMQ container via Testcontainers. Mercado Pago
HTTP calls are intercepted by WireMock.Net so the suite never reaches the real sandbox.

Coverage threshold enforced in CI: ≥80% line coverage.

## Architecture

**Clean Architecture + DDD + CQRS**, strict dependency rule: `Domain ← Application ← Infrastructure`, `Api → all`.

```text
Api (Minimal API)
 └─► Application (CQRS / MediatR)
      └─► Domain (pure business logic)
Infrastructure (MongoDB driver, MassTransit, Outbox, Mercado Pago client)
 └─► Application (implements ports)
```

### Projects

| Project                                     | Role                                                                                                  |
| :------------------------------------------ | :---------------------------------------------------------------------------------------------------- |
| `Mecanica.Hermes.Pagamento.Domain`          | Aggregate `Pagamento`, value objects, domain events, state classes, `Result<T>`                       |
| `Mecanica.Hermes.Pagamento.Application`     | CQRS commands/queries, MassTransit consumers, ports (interfaces)                                      |
| `Mecanica.Hermes.Pagamento.Infrastructure`  | MongoDB driver, repositories, Outbox processor, MassTransit setup, Mercado Pago client, observability |
| `Mecanica.Hermes.Pagamento.Api`             | Minimal API endpoints, middleware, `ResultPresenter`, OpenAPI/Scalar                                  |
| `Mecanica.Hermes.Pagamento.TestUtils`       | Shared test builders and fakes (not a runnable test project)                                          |

### Domain: Pagamento

The aggregate root `Pagamento` (payment) manages the lifecycle of a Mercado Pago payment link.
Key sub-entities:

- `PagamentoHistoricoStatus` — audit log of each state transition

**State Pattern** — each status is a concrete class inheriting `PagamentoStatusBase`:

```text
PendenteGeracao
    │ GerarLink (calls Mercado Pago)
    ▼
LinkGerado
    │ MarcarEmailEnviado (after EnviarEmailPagamentoCommand published)
    ▼
AguardandoConfirmacao
    │ Confirmar  ─► Pago      (final)
    │ Recusar    ─► Recusado  (final)
    │ Expirar    ─► Expirado  (final)
```

State classes define `GerarLink()`, `MarcarEmailEnviado()`, `Confirmar()`, `Recusar()`, and
`Expirar()` returning `Result<PagamentoStatusBase>`.

### Result Pattern

All domain and application operations return `Result` or `Result<T>`. Results carry
`HttpStatusCode`, `IsSuccess`/`IsFailure`, and `Errors`. The `ResultPresenter` in the API layer
converts `Result<T>` to the appropriate `IResult` HTTP response (RFC 7807 problem details on failure).

### CQRS Flow

**Write (async commands):**

1. Route handler creates a command and sends via `ISender` (MediatR).
2. The generic `AsyncCommandHandler<T>` publishes the command as a message via
   `IAsyncMessagePublisher` (MassTransit adapter).
3. Returns `Result.Accepted()` immediately to the caller.
4. A MassTransit consumer (`PagamentoConsumerBase<TCommand>`) processes the message, loads the
   aggregate, calls the domain method, then saves via `IUnitOfWork`.

**Read (sync queries):**
Query handlers call the repository directly and return `Result<T>` synchronously.

### Outbox Pattern (MongoDB)

Domain events raised inside an aggregate are:

1. Serialized into the `pagamento_outbox` collection in the **same Mongo session/transaction** as
   the aggregate save (atomicity guaranteed via replica set + `IClientSessionHandle`).
2. Picked up by the `MongoOutboxProcessor` background service, which uses `findOneAndUpdate`
   (atomic claim with TTL fallback) to dispatch them via `DomainEventDispatcher` → MediatR
   notification → MassTransit publish.

This ensures at-least-once event delivery even if the message broker is temporarily unavailable.

### Infrastructure: Document/Domain Split

MongoDB-mapped classes live in `Infrastructure/Persistence/Entities/` (e.g., `PagamentoDocument`).
These are separate from domain classes and mapped via the `PagamentoMapper` extension methods
inside Infrastructure. Domain classes have **no persistence attributes**.

### SAGA & Polling

The `PagamentoStateMachine` (MassTransit) coordinates one operation at a time per `PagamentoId`,
similar to the OS service. Two extra mechanisms specific to payments:

1. **Webhook reconciliation** — `MercadoPagoWebhookConsumer` validates HMAC, deduplicates by
   `mp_event_id`, and publishes `MercadoPagoCallbackRecebidoRequest` into the saga.
2. **Polling fallback** — when the saga enters `AguardandoConfirmacao`, it schedules a
   `VerificarStatusPagamentoRequest` via `IMessageScheduler` (default 2 minutes). If the MP API
   responds `pending`, the saga reschedules until `LimiteVerificacoes` is reached. This way the
   payment confirmation does not depend solely on webhook delivery.

When the saga confirms approval, it publishes
`Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration.PagamentoAprovadoRequest`
— a `record (Guid OrdemDeServicoId)` that matches the contract consumed by the OS saga.

### Cross-service Integration Events

4 contratos cross-service publicados/consumidos por esta API. Todos versionados com sufixo `.v1`
no `[EntityName]` (kebab-case):

| Direção | Evento (`EntityName`) | Quando |
|---|---|---|
| Consome ← | `ordem-de-servico.aguardando-pagamento.v1` | OS pediu pagamento — gera link MP |
| Publica → | `link-pagamento-gerado.v1` | Após `LinkGerado`, dispara envio do email |
| Publica → | `pagamento.confirmado.v1` | SAGA confirma `Pago` (via webhook ou polling) |
| Publica → | `pagamento.recusado.v1` | SAGA confirma `Recusado` ou `Expirado` |

**Regra de evolução**: nunca alterar a forma de um evento publicado — sempre criar `.v2` em
paralelo e drenar `.v1` antes de removê-lo.

### Endpoint Registration Pattern

Routes are static extension methods, grouped under `PagamentosEndpoints.cs`:

```csharp
// Program.cs
app.MapPagamentosEndpoints();
app.MapWebhooksEndpoints();

// PagamentosEndpoints.cs
var group = app.MapGroup("/api/pagamentos");
group.MapGerarPagamento();
group.MapConsultarPagamento();
// ...

// Routes/GerarPagamentoRoute.cs
endpoints.MapPost("/", GerarPagamento)
    .RequireAuthorization(AuthPolicies.OnlyAdminScope);
```

Each handler: parse request → build Command/Query → `sender.Send(...)` → `response.Present()`.

### Authentication

JWT Bearer authentication with scope-based authorization (`AuthPolicies`). In `Development`
and `Testing` environments, `DevelopmentAuthenticationMiddleware` bypasses JWT verification so
endpoints can be called without a real token.

The `POST /api/webhooks/mercadopago` endpoint is **public** but signature-validated via the HMAC
secret stored in `MERCADO_PAGO__WEBHOOK_SECRET`.

### Repository Interfaces (ISP)

- `IPagamentoReader` — query-only methods (used by query handlers)
- `IPagamentoWriter` — mutation methods (used by command consumers)
- `IPagamentoRepository` — combines both (implemented by `PagamentoRepository`)

Query handlers only depend on `IPagamentoReader` to prevent accidental writes.

### Key Dependencies

| Concern         | Package                                                  |
| :-------------- | :------------------------------------------------------- |
| CQRS mediator   | MediatR 14                                               |
| Async messaging | MassTransit 8 (RabbitMQ / SQS) + MongoDb saga repository |
| Object mapping  | Manual extension methods                                 |
| DI container    | Autofac 9                                                |
| Database        | MongoDB.Driver 3 (replica set required for transactions) |
| Payment gateway | mercadopago-sdk + Polly resilience                       |
| API docs        | Scalar + Microsoft.AspNetCore.OpenApi                    |
| Observability   | OpenTelemetry (OTLP → New Relic) + Serilog               |
| Testing         | xUnit, Moq, FluentAssertions, Bogus, Testcontainers, WireMock.Net |

## Testing Strategy

- **`Domain.Tests`** — pure unit tests, no mocks needed (domain is side-effect free).
- **`Application.Tests`** — unit tests with Moq; use `MassTransit.TestFramework` `InMemoryTestHarness`
  for consumer tests.
- **`Infrastructure.Tests`** — test repositories against Testcontainers MongoDB; SAGA against
  `InMemoryTestHarness`; Mercado Pago client against `WireMock.Net`.
- **`Api.Tests`** — middleware and presenter unit tests.
- **`IntegrationTests`** — full HTTP stack using `WebApplicationFactory` + Testcontainers MongoDB +
  Testcontainers RabbitMQ + WireMock.Net for Mercado Pago.
- **`TestUtils`** — `PagamentoBuilder` and other Bogus-powered builders shared across test projects.

Consumer tests follow the Template Method pattern: only override `Execute()` and `ValidatePreconditions()`.

## Docs

Detailed documentation lives in `docs/`:

- `architecture.md` — Layer dependency diagram and patterns
- `payment-flow.md` — Full state machine, webhook + polling reconciliation
- `database.md` — Mongo collections, indexes, document layout
- `testing.md` — Test strategy and conventions
- `ci-cd.md` — GitHub Actions and Docker multi-platform build
- `adrs.md` — Architecture Decision Records
- `messaging.md` — SAGA, Outbox, message contracts, consumer pipeline
- `api-reference.md` — Endpoints, auth scopes, async semantics
- `configuration.md` — Environment variables
- `observability.md` — Metrics, traces, logs
- `runbook.md` — Operational incidents
- `contributing.md` — Contributor guide
