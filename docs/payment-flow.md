# Fluxo de Pagamento

## Máquina de estados

```
PendenteGeracao
    │ GerarLink (chama Mercado Pago, cria preference)
    ▼
LinkGerado
    │ MarcarEmailEnviado (após publish do EnviarEmailPagamentoCommand)
    ▼
AguardandoConfirmacao
    │ Confirmar(via)  ─► Pago      (final)
    │ Recusar(motivo) ─► Recusado  (final)
    │ Expirar         ─► Expirado  (final)
```

`PendenteGeracao` e `LinkGerado` também aceitam `Expirar` (timeout de geração ou link vencido).
`Pago`, `Recusado` e `Expirado` são terminais (`IsTerminal == true`).

## Ciclo end-to-end

1. **`POST /api/pagamentos`** → API gera `PagamentoId` e despacha `GerarPagamentoCommand` via MediatR → `AsyncCommandHandler` publica `GerarPagamentoRequest` no bus → retorna `202 Accepted`.
2. **SAGA** correlaciona por `PagamentoId`, agenda timeout de 5 min, publica `ExecuteGerarLinkPagamento`, transita para `Processing`.
3. **`GerarLinkPagamentoConsumer`** cria o agregado, chama o Mercado Pago, aplica `GerarLink` + `MarcarEmailEnviado`, persiste, publica `EnviarEmailPagamentoCommand` e agenda o **primeiro polling** (`VerificarStatusPagamentoRequest` daqui a `PAGAMENTO__POLLING_INTERVAL`).
4. **Confirmação dupla:**
   - **Webhook:** `POST /api/webhooks/mercadopago` valida HMAC + idempotência, publica `MercadoPagoCallbackRecebidoRequest`. SAGA dispara `ExecuteMercadoPagoCallback`. `MercadoPagoCallbackConsumer` confirma/recusa o pagamento e publica `PagamentoAprovadoRequest` (cross-service).
   - **Polling:** `VerificarStatusPagamentoConsumer` consulta o MP. Se ainda `pending`, reagenda outra verificação enquanto `TentativasVerificacao < LimiteVerificacoes`. Quando atinge o limite, expira o pagamento.
5. **Callback à OS:** ao confirmar, publica `PagamentoAprovadoRequest { OrdemDeServicoId }` no bus — a SAGA da OS finaliza o ciclo (`AguardandoPagamento → PagamentoConfirmado`).

## Idempotência

- **Webhook:** unique constraint em `webhook_events.MercadoPagoEventId`. Retransmissões caem fora silenciosamente.
- **Mercado Pago:** header `X-Idempotency-Key = PagamentoId` na criação da preference para tolerar retries.
- **Confirmação:** se `MercadoPagoCallbackConsumer` recebe webhook approved e o agregado já está `Pago`, apenas reemite o `PagamentoAprovadoRequest` (defensivo) e completa.

## Timeout sentinel

A SAGA agenda `PagamentoOperationTimedOut` ao entrar em `Processing`. Após `OperationTimeoutDelay` (default 5 min), se não houver `OperationCompleted`/`Failed`, a SAGA volta para `Initial` registrando o motivo no `UltimoMotivoFalha` da instância da SAGA.
