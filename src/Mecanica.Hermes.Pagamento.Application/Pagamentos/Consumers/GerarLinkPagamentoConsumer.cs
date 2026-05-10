using System.Text.Json;
using MassTransit;
using Mecanica.Hermes.Contracts.Integration;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Constants;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Shared.Application.Messaging;
using Mecanica.Hermes.Shared.Core.Persistence;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;

/// <summary>
///     Consumer responsável pela criação inicial do agregado e geração do link de pagamento.
///     Etapas:
///     <list type="number">
///         <item>Constrói o agregado <c>Pagamento</c> via factory de domínio.</item>
///         <item>Chama o Mercado Pago para criar a preference (link de pagamento).</item>
///         <item>Aplica <c>GerarLink</c> e <c>MarcarEmailEnviado</c> no agregado.</item>
///         <item>Publica <see cref="LinkPagamentoGeradoEvent" /> para a lambda de e-mail.</item>
///         <item>Agenda o primeiro <see cref="VerificarStatusPagamentoRequest" /> (polling).</item>
///         <item>Persiste e publica <c>PagamentoOperationCompleted</c>.</item>
///     </list>
/// </summary>
public sealed class GerarLinkPagamentoConsumer(
    IPagamentoRepository repository,
    IUnitOfWork unitOfWork,
    IMercadoPagoClient mercadoPagoClient,
    IPagamentoMetrics metrics,
    IPagamentoSchedulerOptions schedulerOptions)
    : PagamentoConsumerBase<ExecuteGerarLinkPagamento>
{
    protected override string OperacaoMetricKey => PagamentoMetricKeys.Operations.GerarLink;

    public override async Task Consume(ConsumeContext<ExecuteGerarLinkPagamento> context)
    {
        var msg = context.Message;

        // Idempotência: se o pagamento já existe (replay/retry), não recria.
        var existente = await repository.GetByIdAsync(msg.PagamentoId);
        if (existente is not null)
        {
            await PublishCompleted(context);
            return;
        }

        var creation = Domain.Pagamentos.Pagamento.Criar(
            msg.PagamentoId,
            msg.OrdemDeServicoId,
            msg.ClienteId,
            msg.EmailCliente,
            msg.NomeCliente,
            msg.Valor,
            msg.Vencimento);

        if (creation.IsFailure)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.ValidationError);
            await PublishFailed(context, creation.Errors);
            return;
        }

        var pagamento = creation.Data!;

        MercadoPagoCreatePreferenceResult preference;
        try
        {
            preference = await mercadoPagoClient.CreatePreferenceAsync(
                pagamento.Id,
                pagamento.EmailCliente.Valor,
                pagamento.NomeCliente,
                pagamento.Valor.Valor,
                pagamento.Valor.Moeda,
                pagamento.Vencimento,
                context.CancellationToken);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Cancelamento deve subir para o bus reagendar via redelivery.
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // HttpRequestException: rede / status != 2xx (após EnsureSuccessStatusCode)
            // TaskCanceledException: timeout do HttpClient (não confundir com cancelamento do bus)
            // JsonException: payload inesperado da API do MP
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.MercadoPagoError);
            await PublishFailed(context, $"Falha ao criar preference no Mercado Pago: {ex.Message}");
            return;
        }

        var gerar = pagamento.GerarLink(preference.PreferenceId, preference.InitPoint);
        if (gerar.IsFailure)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.DomainError);
            await PublishFailed(context, gerar.Errors);
            return;
        }

        var marcar = pagamento.MarcarEmailEnviado();
        if (marcar.IsFailure)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.DomainError);
            await PublishFailed(context, marcar.Errors);
            return;
        }

        await repository.AddAsync(pagamento);
        await unitOfWork.CommitAsync(context.CancellationToken);

        await context.Publish(new LinkPagamentoGeradoEvent(pagamento.Id));

        // Agenda o primeiro polling para reconciliar caso o webhook não chegue.
        // O scheduler usa o DelayedMessageScheduler do RabbitMQ.
        await context.SchedulePublish(
            schedulerOptions.PollingInterval,
            new VerificarStatusPagamentoRequest(pagamento.Id));

        metrics.PagamentoCriado();

        await PublishCompleted(context);
    }
}
