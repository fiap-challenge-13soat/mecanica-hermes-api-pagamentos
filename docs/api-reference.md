# Referência da API

Base path: `/api/pagamentos` e `/api/webhooks/mercadopago`.

## Endpoints

### `POST /api/pagamentos` *(admin)*

Solicita a criação de um pagamento. Resposta `202 Accepted` com o `Id` gerado.

#### **Body**

```json
{
  "ordemDeServicoId": "guid",
  "clienteId": "guid",
  "emailCliente": "joao@example.com",
  "nomeCliente": "João da Silva",
  "valor": 1234.56,
  "vencimento": "2026-05-10T00:00:00Z"
}
```

#### **Resposta**

```json
{ "id": "guid-do-pagamento" }
```

### `GET /api/pagamentos/{id}` *(cliente / admin)*

Retorna o pagamento com seu status atual e histórico de transições.

### `GET /api/pagamentos/{id}/status` *(admin)*

Força uma reconciliação imediata do status no Mercado Pago. Idempotente — aciona o mesmo
caminho do polling agendado.

### `POST /api/webhooks/mercadopago` *(público + HMAC)*

Recebe notificações do Mercado Pago. Validações:

1. `x-signature` (HMAC SHA-256 com `MERCADO_PAGO__WEBHOOK_SECRET`).
2. Idempotência via `x-request-id` (chave em `webhook_events`).
3. `data.id` deve ser um GUID — interpretado como `PagamentoId` (alinha com `external_reference` da preference).

Resposta `202 Accepted` em caso de sucesso, `401 Unauthorized` para assinatura inválida,
`400 Bad Request` para payload mal-formado.

### `GET /healthz/live` & `/healthz/ready`

Health checks. `live` apenas verifica que o processo responde; `ready` agrega os checks de
infraestrutura (Mongo, MassTransit). Resposta `200`/`503` por agregação.

## Semântica assíncrona

- Endpoints de mutação retornam `202 Accepted` — não esperam o consumer concluir.
- O cliente deve fazer polling em `GET /api/pagamentos/{id}` para conferir o `statusAtual`.
- Em caso de aprovação, a API de OS é notificada em segundo plano via `PagamentoAprovadoRequest` no bus.

## Auth

JWT Bearer com scope claim. Em `Development`/`Testing`, o `DevelopmentAuthenticationMiddleware`
substitui a verificação por uma identidade fake com ambos os scopes.
