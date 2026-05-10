# Observabilidade

OpenTelemetry + Serilog. Exporta via OTLP (HTTP/protobuf por padrão). Testado contra New Relic.

## Métricas — Meter `MecanicaHermes.Pagamento`

| Métrica | Tipo | Labels |
| --- | --- | --- |
| `pagamento.criado` | Counter | — |
| `pagamento.confirmado` | Counter | `via` (`Webhook` / `Polling`) |
| `pagamento.recusado` | Counter | — |
| `pagamento.expirado` | Counter | — |
| `pagamento.erros` | Counter | `operacao`, `tipo_erro` |
| `mercado_pago.webhook.recebido` | Counter | `outcome` (`approved`/`rejected`/`pending`/`duplicate`) |
| `mercado_pago.preference.criada.duracao_ms` | Histogram | — |
| `mercado_pago.consulta.duracao_ms` | Histogram | — |

## Métricas — Meter `MecanicaHermes.Pagamento.Outbox`

- `outbox.messages.dispatched`
- `outbox.messages.failed`
- `outbox.messages.dead_lettered`

Mais métricas built-in: `MassTransit`, `runtime`, ASP.NET Core, HttpClient.

## Traces — ActivitySource `MecanicaHermes.Pagamento.Api` e `MecanicaHermes.Pagamento.Application`

- Cada request MediatR cria um span via `TracingBehavior`, com `mediator.request` e
  (quando aplicável) `pagamento.id` como tags.
- Cada despacho do Outbox cria um span `outbox.dispatch/{type}` propagando o `TraceContext`
  capturado no momento do commit (continuação da cadeia entre a request originadora e o despacho assíncrono).
- O MassTransit instrumenta automaticamente publishes e consumes.

## Logs — Serilog

- Logger root: `MecanicaHermes.Pagamento`.
- Enrichers: `Span` (TraceId/SpanId), `LogLevelEnricher` (LogLevelNumber).
- `CorrelationId` é injetado pelo `CorrelationIdMiddleware` via `LogContext.PushProperty`.
- Logs de `/health*` são filtrados para não poluir o sink remoto.
- Quando `OBSERVABILITY__ENABLED=true`, exporta via `Serilog.Sinks.OpenTelemetry`.
