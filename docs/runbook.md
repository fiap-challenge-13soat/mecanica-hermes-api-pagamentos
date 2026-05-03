# Runbook

## Sintoma → Causa provável → Ação

### `outbox.messages.dead_lettered` aumentando

Mensagens não estão sendo entregues. Causas comuns:
- Tipo do evento renomeado/movido sem manter `[DomainEventName]` estável.
- Schema do payload alterado de forma incompatível com o handler.

**Ação:**
1. Inspecionar `pagamento_outbox` filtrando por `Status = 2`. Olhar `LastError`.
2. Reprocessar manualmente após corrigir o handler: `db.pagamento_outbox.updateMany({Status:2}, {$set:{Status:0, RetryCount:0, ClaimedBy:null, ClaimedAt:null}})`.

### Pagamentos travados em `AguardandoConfirmacao`

Webhook não chegou e polling não está rodando.

**Ação:**
1. Verificar logs do `VerificarStatusPagamentoConsumer` — está sendo agendado?
2. Conferir saúde do RabbitMQ / `delayed_message_exchange` plugin.
3. Forçar reconciliação manual: `GET /api/pagamentos/{id}/status` (admin scope).

### `mercado_pago.webhook.recebido{outcome=duplicate}` muito alto

MP está retransmitindo eventos. Normal em volume controlado; alarme se exceder ~30% das requests.

**Ação:**
1. Garantir que `POST /api/webhooks/mercadopago` está retornando `202` rapidamente — atrasos disparam reentrega.
2. Verificar se o TTL do `webhook_events` (90d) foi alterado.

### Saga travada em `Processing`

Consumer crashou antes de publicar `OperationCompleted/Failed`.

**Ação:**
- O timeout sentinel (`OperationTimeoutDelay = 5min`) volta a SAGA para `Initial` automaticamente.
- Em caso de loop, inspecionar `pagamento-saga-state` e — se necessário — resetar manualmente o `CurrentState` para `Initial` e limpar `OperacaoEmAndamento`.

### Erros `ConcurrencyConflict`

Uma operação concorrente atualizou o agregado entre o read e o write. **Comportamento esperado** — a política de retry do MassTransit reentregará a mensagem e o consumer recarregará o estado atual.

Se persistir indefinidamente para o mesmo `PagamentoId`, há um bug no consumer (provavelmente
estado inválido para a operação solicitada). Inspecionar logs e DLQ.
