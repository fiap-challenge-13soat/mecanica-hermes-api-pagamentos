# ADRs — Architecture Decision Records

> Espelha o estilo da OS. Atualizar quando uma decisão arquitetural relevante for tomada.

## ADR-001 — MongoDB como banco principal (sem EF Core)

**Status:** Aceito.

**Contexto:** A OS usa Postgres + EF Core. Para o serviço de Pagamentos optamos por MongoDB
(driver oficial 3.x). O agregado tem schema fluido (extensão futura para múltiplos meios de
pagamento, dados específicos do MP) e o ciclo de leitura/escrita é centrado em Id (lookup direto).

**Consequência:** Sem migrations relacionais. Optimistic locking via campo `version` na coleção.
Outbox transacional usa atomicidade de single-document + claim com TTL fallback.

## ADR-002 — Outbox em coleção MongoDB com claim atômico

**Status:** Aceito.

**Contexto:** A OS usa Postgres com `FOR UPDATE SKIP LOCKED`. Em Mongo, transações multi-doc
exigem replica set, e o equivalente mais idiomático é `findOneAndUpdate` para reivindicar
documentos individualmente.

**Decisão:** `MongoOutboxProcessor` reivindica até `BatchSize` documentos via loop de
`findOneAndUpdate(filter: status=Pending AND (claimedBy=null OR claimedAt<ttl), set: claimedBy, claimedAt)`.
Documentos órfãos (cuja claim expirou) podem ser reaproveitados, dando defesa contra travamentos.

**Trade-off:** Em alta concorrência o overhead de N FindOneAndUpdate é maior que um
`SELECT … FOR UPDATE SKIP LOCKED LIMIT N` do Postgres. Aceitável dado o volume esperado de
domain events do agregado Pagamento.

## ADR-003 — Polling agendado via `IMessageScheduler`

**Status:** Aceito.

**Contexto:** Webhook do Mercado Pago pode falhar. Precisamos de mecanismo de reconciliação.

**Decisão:** Em vez de um worker .NET dedicado, usamos `MassTransit.SchedulePublish` com o
`UseDelayedMessageScheduler` do RabbitMQ (plugin `rabbitmq_delayed_message_exchange`). O
`GerarLinkPagamentoConsumer` agenda o primeiro polling após `PAGAMENTO__POLLING_INTERVAL`.
O `VerificarStatusPagamentoConsumer` reagenda enquanto `TentativasVerificacao < LimiteVerificacoes`.

**Consequência:** Eliminamos um BackgroundService dedicado, ganhamos resiliência (mensagem
agendada sobrevive a crashes do worker), e o limite de tentativas é responsabilidade do agregado.

## ADR-004 — Contrato `PagamentoAprovadoRequest` no namespace da OS

**Status:** Aceito (provisório — substituir por lib de contratos compartilhada — ver ADR-005).

**Contexto:** O MassTransit roteia mensagens por `MessageUrn` derivada de `Namespace + TypeName`.
A SAGA da OS aguarda o tipo `Mecanica.Hermes.Application.OrdensDeServico.Messages.Requests.PagamentoAprovadoRequest`.

**Decisão:** Criar a classe em `src/Mecanica.Hermes.Pagamento.Application/Pagamentos/Messages/Integration/PagamentoAprovadoRequest.cs`
com `namespace` deliberadamente alinhado ao da OS. Comentário explícito no arquivo deixa
clara a razão.

## ADR-005 — Extrair `Mecanica.Hermes.Contracts.OrdemDeServico` *(planejado)*

**Status:** Proposto — ver ADR-017 da OS.

**Decisão:** Antes de subir Pagamentos para produção, extrair os contratos cross-service em
uma lib NuGet versionada para que os dois serviços não dupliquem o `record`.

## ADR-006 — Result Pattern para erros de domínio

Idêntico à decisão da OS. Mantido aqui para que o serviço continue legível em isolamento.

## ADR-007 — State Pattern para o ciclo de vida do Pagamento

Idem OS. Cada estado é uma classe com métodos `GerarLink`/`Confirmar`/`Recusar`/`Expirar`
retornando `Result<PagamentoStatusBase>` — transições inválidas são detectadas em compile-time + runtime.
