# Contribuindo

## Setup

```bash
# instalar SDK 10
# instalar docker para os testes de integração
# (opcional) instalar just para os atalhos
just build
just test-unit
```

## Convenções

- **Estilo:** `dotnet format` deve passar; max line length **120 colunas** (`.editorconfig`).
- **Naming:** classes em PascalCase, campos privados em `_camelCase`, namespace = caminho.
- **Testes:** todo PR que adiciona código de produção deve incluir testes (cobertura ≥ 80%).
- **Commits:** mensagens em português ou inglês, modo imperativo. Pequenos e focados.
- **PRs:** descrever o "porquê", não só o "o quê". Linkar ADR quando aplicável.

## Adicionando um novo Execute command + Consumer

1. Crie o `IPagamentoExecuteCommand` em `Application/Pagamentos/Messages/Commands/`.
2. Adicione um `When(...)` na SAGA `PagamentoStateMachine` que publica esse Execute.
3. Crie o consumer herdando de `PagamentoConsumerBase<TCommand>`. Implemente `Consume(ctx)`.
4. Publique `PagamentoOperationCompleted` em sucesso, `PagamentoOperationFailed` em erro.
5. Acrescente uma chave em `PagamentoMetricKeys.Operations`.
6. Escreva pelo menos um teste com `MassTransit.TestFramework`.

## Mudanças em Domain Events

- Use `[DomainEventName("...v2")]` para criar uma nova versão do evento.
- Mantenha a v1 capaz de deserializar enquanto houver mensagens antigas no Outbox/DLQ.
