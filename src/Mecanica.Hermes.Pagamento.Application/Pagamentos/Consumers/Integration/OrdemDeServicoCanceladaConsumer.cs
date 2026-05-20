using MassTransit;
using Mecanica.Hermes.Contracts.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Shared.Core.Persistence;
using Microsoft.Extensions.Logging;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers.Integration;

/// <summary>
///     Reage ao cancelamento de uma Ordem de Serviço cancelando/recusando o pagamento ativo
///     associado, se existir. Sem este consumer, um pagamento ficaria órfão em
///     <c>AguardandoConfirmacao</c> mesmo após a OS ser cancelada — o polling MP continuaria
///     consumindo recursos e o cliente poderia efetivamente pagar uma OS já fechada,
///     causando inconsistência cross-service.
///     <para>
///         Idempotente: se não há pagamento ativo (já em estado terminal ou inexistente),
///         o consumer retorna sem efeito colateral.
///     </para>
/// </summary>
public sealed partial class OrdemDeServicoCanceladaConsumer(
    IPagamentoRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<OrdemDeServicoCanceladaConsumer> logger)
    : IConsumer<OrdemDeServicoCanceladaEvent>
{
    public async Task Consume(ConsumeContext<OrdemDeServicoCanceladaEvent> context)
    {
        var msg = context.Message;
        var pagamento = await repository.GetByOrdemDeServicoIdAsync(msg.OrdemDeServicoId);
        if (pagamento is null)
        {
            LogSemPagamento(logger, msg.OrdemDeServicoId);
            return;
        }

        if (pagamento.StatusAtual.IsTerminal)
        {
            LogJaTerminal(logger, msg.OrdemDeServicoId, pagamento.StatusAtual.StatusAtual);
            return;
        }

        var motivo = $"Ordem de Serviço cancelada (estava em '{msg.StatusAnterior}').";
        var recusar = pagamento.Recusar(motivo);
        if (recusar.IsFailure)
        {
            LogFalhaTransicao(logger, pagamento.Id, recusar.Errors.First());
            return;
        }

        await repository.UpdateAsync(pagamento);
        await unitOfWork.CommitAsync(context.CancellationToken);

        LogPagamentoCancelado(logger, pagamento.Id, msg.OrdemDeServicoId);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "OS cancelada {OrdemDeServicoId} sem pagamento associado — nada a fazer.")]
    private static partial void LogSemPagamento(ILogger logger, Guid ordemDeServicoId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "OS cancelada {OrdemDeServicoId} mas pagamento em estado terminal {Status} - nada a fazer.")]
    private static partial void LogJaTerminal(ILogger logger, Guid ordemDeServicoId, PagamentoStatus status);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao recusar pagamento {PagamentoId} em resposta a OS cancelada: {Erro}")]
    private static partial void LogFalhaTransicao(ILogger logger, Guid pagamentoId, string erro);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Pagamento {PagamentoId} recusado em resposta ao cancelamento da OS {OrdemDeServicoId}.")]
    private static partial void LogPagamentoCancelado(ILogger logger, Guid pagamentoId, Guid ordemDeServicoId);
}
