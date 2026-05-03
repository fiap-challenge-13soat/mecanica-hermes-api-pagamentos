# Banco de Dados

MongoDB 7+. Requer **replica set** para suporte a transações multi-documento (DocumentDB também
serve, desde que o cluster esteja configurado em modo replica set).

## Coleções

### `pagamentos`

Documento BSON com a snapshot atual do agregado. Indexes:

- `_id` — primary key (Guid).
- `ix_pagamentos_ordem_de_servico` — `OrdemDeServicoId` (consulta por OS).
- `ix_pagamentos_status_atual` — `StatusAtual` (filtros e dashboards).

Optimistic locking via campo `version` (incrementado a cada `UpdateAsync`). O repositório usa
`ReplaceOne(filter: id+version_atual)` e lança `ConcurrencyConflictException` quando
`MatchedCount == 0`.

### `pagamento_outbox`

Mensagens pendentes do Outbox. Indexes:

- `_id` — primary key (Guid).
- `ix_outbox_status_occurredon` — `(Status, OccurredOn)` para o claim do processor.

Estados: `Pending = 0`, `Processed = 1`, `DeadLettered = 2`.

### `webhook_events`

Idempotência de webhooks do Mercado Pago. `_id = MercadoPagoEventId` (string). TTL index em
`ReceivedAt` com `ExpireAfter = 90d` para limpeza automática.

### `pagamento-saga-state` (banco `MONGODB__SAGA_DATABASE`)

Estado da `PagamentoStateMachine`. Gerenciado pelo `MassTransit.MongoDb` automaticamente.

## Inicialização de índices

`MongoIndexInitializer` (registrado como `IHostedService`) cria todos os índices no startup.
Operação idempotente — o driver pula índices pré-existentes com a mesma definição.

Para ambientes onde a criação é feita externamente (operação por DBA), defina
`OBSERVABILITY__ENABLED=false` (placeholder — a versão atual sempre roda o initializer).

## Convenções BSON

- `GuidRepresentationMode.V3` (UUID padrão BSON 4 binary subtype 4).
- `EnumRepresentationConvention(BsonType.String)` — enums serializados como string.
- `CamelCaseElementNameConvention` — campos em camelCase no BSON, mantendo PascalCase nos POCOs C#.
- `IgnoreExtraElementsConvention(true)` — campos extras nos documentos não causam falha (evolução de schema sem quebrar leitura).
