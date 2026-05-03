# Mensageria

## SAGA `PagamentoStateMachine`

| Estado | Eventos aceitos |
| --- | --- |
| `Initial` | `GerarPagamentoRequest`, `MercadoPagoCallbackRecebidoRequest`, `VerificarStatusPagamentoRequest` |
| `Processing` | Apenas `OperationCompleted/Failed/TimedOut`; novas requests são descartadas (`Ignore`) |

A correlação é feita por `PagamentoId` (CorrelationId). Persistência da SAGA: `MongoDbRepository`
da `MassTransit.MongoDb` na coleção `pagamento-saga-state` (database `MONGODB__SAGA_DATABASE`).

## Contratos

### Requests (HTTP/Webhook → SAGA)

- `GerarPagamentoRequest(PagamentoId, OrdemDeServicoId, ClienteId, EmailCliente, NomeCliente, Valor, Vencimento)`
- `MercadoPagoCallbackRecebidoRequest(PagamentoId, MercadoPagoEventId, MercadoPagoStatus)`
- `VerificarStatusPagamentoRequest(PagamentoId)` (delayed, agendado pelos consumers)

### Execute commands (SAGA → Consumer)

- `ExecuteGerarLinkPagamento`
- `ExecuteMercadoPagoCallback`
- `ExecuteConfirmarPagamento(PagamentoId, Via)`
- `ExecuteRecusarPagamento(PagamentoId, Motivo)`
- `ExecuteExpirarPagamento(PagamentoId)`
- `ExecuteVerificarStatusPagamento(PagamentoId)`

### Events (Consumer → SAGA)

- `PagamentoOperationCompleted(PagamentoId, Operacao)`
- `PagamentoOperationFailed(PagamentoId, Operacao, Motivo)`
- `PagamentoOperationTimedOut(PagamentoId, Operacao)` (agendado)

### Integration (cross-service)

- **`Mecanica.Hermes.Application.OrdensDeServico.Messages.Requests.PagamentoAprovadoRequest(OrdemDeServicoId)`** — namespace **idêntico** ao da API de OS para garantir roteamento MassTransit por `MessageUrn`.
- `EnviarEmailPagamentoCommand(...)` — consumido pela lambda de e-mail (futura).
- `PagamentoRecusadoIntegrationEvent(...)` — para futura API de clientes.

## Outbox em MongoDB

Coleção `pagamento_outbox` com schema `{ _id, type, payload, status, retryCount, lastError, createdAt, processedAt, claimedBy, claimedAt, traceContext }`.

- **Claim atômico:** `MongoOutboxProcessor` faz `FindOneAndUpdate(status=Pending, ClaimedBy=null OR ClaimedAt<ttl)` setando o instance id e timestamp. Múltiplas réplicas competem sem double-delivery.
- **DLQ:** após `MaxRetries` consecutivas, status vira `DeadLettered` e a mensagem não é reprocessada.
- **Cleanup:** `MongoOutboxCleanupService` remove documentos `Processed` mais antigos que `OUTBOX__PROCESSED_RETENTION_DAYS` (default 7d).
- **Discriminador estável:** `[DomainEventName("...")]` no Domain Event grava um nome lógico em vez de `AssemblyQualifiedName`.

## Pipeline do Consumer (MassTransit)

- `UseMessageRetry` — retry imediato 3× com 2s para `TimeoutException`/`HttpRequestException`.
- `UseDelayedRedelivery` — exponencial (15s → 2min, 3 tentativas) para falhas mais persistentes.
- `UseInMemoryOutbox` — garantia "tudo ou nada" entre publish + commit dentro do consumer.
- `PagamentoCommandValidationFilter<T>` — descarta mensagens com `PagamentoId == Guid.Empty` sem retry.
