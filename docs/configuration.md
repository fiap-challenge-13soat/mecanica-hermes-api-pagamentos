# Configuração

Todas as variáveis usam **double-underscore** (`__`) para mapeamento ASP.NET Core.

## Runtime

| Variável | Default | Descrição |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | — | `Development` / `Testing` / `Production`. |
| `ASPNETCORE_HTTP_PORTS` | `8080` | Porta HTTP (definida pelo Dockerfile). |

## Persistência

| Variável | Default | Descrição |
| --- | --- | --- |
| `MONGODB__CONNECTION_STRING` | `mongodb://localhost:27017` | Connection string do Mongo (replica set obrigatório p/ transações). |
| `MONGODB__DATABASE` | `mechermes_pagamento` | Banco do agregado + outbox + webhooks. |
| `MONGODB__SAGA_DATABASE` | `mechermes_pagamento_saga` | Banco do estado da SAGA. |

## Mensageria

| Variável | Default | Descrição |
| --- | --- | --- |
| `MASSTRANSIT__TRANSPORT_SERVICE` | (vazio → in-memory) | `Amazon SQS` (recomendado em prod). |
| `MASSTRANSIT__CONNECTION_STRING` | — | URL do RabbitMQ (`amqp://...`). |
| `MASSTRANSIT__PREFETCH_COUNT` | `24` | Prefetch por endpoint. |
| `MASSTRANSIT__QUEUE_NAME_PREFIX` | `mecanica-hermes-pagamento` | Prefixo dos endpoints. |

## Mercado Pago

| Variável | Default | Descrição |
| --- | --- | --- |
| `MERCADO_PAGO__ACCESS_TOKEN` | — | Token de acesso (sandbox ou prod). |
| `MERCADO_PAGO__PUBLIC_KEY` | — | Public key (não usado no backend). |
| `MERCADO_PAGO__WEBHOOK_SECRET` | — | Secret HMAC do webhook. |
| `MERCADO_PAGO__NOTIFICATION_URL` | — | URL pública do webhook. |
| `MERCADO_PAGO__SUCCESS_URL` | — | Redirect pós-pagamento. |
| `MERCADO_PAGO__FAILURE_URL` | — | Redirect em falha. |
| `MERCADO_PAGO__PENDING_URL` | — | Redirect em pendência. |

## Comportamento de pagamento

| Variável | Default | Descrição |
| --- | --- | --- |
| `PAGAMENTO__POLLING_INTERVAL` | `00:02:00` | Intervalo entre verificações no MP. |
| `PAGAMENTO__POLLING_MAX_ATTEMPTS` | `15` | Limite total de tentativas antes de expirar. |
| `PAGAMENTO__OPERATION_TIMEOUT` | `00:05:00` | Timeout sentinel da SAGA. |
| `PAGAMENTO__LINK_VALIDADE` | `24:00:00` | Tempo de validade do link enviado ao cliente. |

## Outbox

| Variável | Default | Descrição |
| --- | --- | --- |
| `OUTBOX__POLL_INTERVAL` | `00:00:02` | Intervalo entre ciclos do processor. |
| `OUTBOX__BATCH_SIZE` | `50` | Documentos reivindicados por ciclo. |
| `OUTBOX__MAX_RETRIES` | `10` | Tentativas antes de DLQ. |
| `OUTBOX__PROCESSED_RETENTION_DAYS` | `7` | Tempo de retenção dos `Processed`. |

## Cadastros API (M2M)

| Variável | Default | Descrição |
| --- | --- | --- |
| `CADASTROS_API__BASE_URL` | `http://localhost:5000` | URL base da API de Cadastros. Usada pelo `ClienteApiClient` para consultar dados do cliente ao criar pagamento. |
| `AUTH__SERVICO_SCOPE` | `servico-scope` | Scope JWT incluído no token M2M para autenticar em Cadastros. O endpoint `GET /api/clientes/{id}` aceita este scope. |

## Auth

| Variável | Default | Descrição |
| --- | --- | --- |
| `AUTH__AUTHORITY` | — | URL do issuer JWT. |
| `AUTH__ISSUER` | — | Valor esperado do claim `iss`. |
| `AUTH__CLIENTE_SCOPE` | `cliente-scope` | Scope que libera leitura. |
| `AUTH__ADMIN_SCOPE` | `admin-scope` | Scope que libera mutação. |

## Observability

| Variável | Default | Descrição |
| --- | --- | --- |
| `OBSERVABILITY__ENABLED` | `false` | Liga OTLP exporter para logs/traces/métricas. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | — | OTLP endpoint (HTTP/protobuf default). |
| `OTEL_EXPORTER_OTLP_HEADERS` | — | Cabeçalhos do exporter (`api-key=...` p/ New Relic). |
| `OTEL_SERVICE_NAME` | `mecanica-hermes-pagamento-api` | Resource attribute. |
