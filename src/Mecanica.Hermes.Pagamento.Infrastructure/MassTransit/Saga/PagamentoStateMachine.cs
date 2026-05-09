using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Events;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Saga;

/// <summary>
///     SAGA que serializa as operações sobre um <c>Pagamento</c>, garantindo que apenas
///     uma operação por <c>PagamentoId</c> seja processada por vez. Mensagens recebidas
///     enquanto o estado é <see cref="Processing" /> são descartadas (Ignore).
///     <para>
///         <b>Timeout sentinel:</b> ao entrar em <c>Processing</c>, um <c>OperationTimeout</c>
///         agendado via <c>DelayedMessageScheduler</c> é disparado após
///         <see cref="OperationTimeoutDelay" />.
///     </para>
///     <para>
///         O reagendamento do polling é responsabilidade do
///         <c>VerificarStatusPagamentoConsumer</c>, não da SAGA — mantém a SAGA simples e
///         determinística.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage(Justification =
    "DSL de saga MassTransit — coberta por testes de integração com InMemoryHarness/RabbitMQ.")]
public class PagamentoStateMachine : MassTransitStateMachine<PagamentoState>
{
    public static readonly TimeSpan OperationTimeoutDelay = TimeSpan.FromMinutes(5);

    public PagamentoStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Schedule(() => OperationTimeout,
            state => state.OperationTimeoutTokenId,
            s =>
            {
                s.Delay = OperationTimeoutDelay;
                s.Received = e => e.CorrelateById(ctx => ctx.Message.PagamentoId);
            });

        ConfigureCorrelations();

        Initially(
            When(GerarPagamentoRequested)
                .StartProcessing(nameof(GerarPagamentoRequested))
                .Schedule(OperationTimeout,
                    ctx => new PagamentoOperationTimedOut(ctx.Saga.CorrelationId, nameof(GerarPagamentoRequested)))
                .Publish(ctx => new ExecuteGerarLinkPagamento(
                    ctx.Saga.CorrelationId,
                    ctx.Message.OrdemDeServicoId,
                    ctx.Message.ClienteId,
                    ctx.Message.EmailCliente,
                    ctx.Message.NomeCliente,
                    ctx.Message.Valor,
                    ctx.Message.Vencimento))
                .TransitionTo(Processing),
            When(MercadoPagoCallbackRecebidoRequested)
                .StartProcessing(nameof(MercadoPagoCallbackRecebidoRequested))
                .Schedule(OperationTimeout,
                    ctx => new PagamentoOperationTimedOut(ctx.Saga.CorrelationId,
                        nameof(MercadoPagoCallbackRecebidoRequested)))
                .Publish(ctx => new ExecuteMercadoPagoCallback(
                    ctx.Saga.CorrelationId,
                    ctx.Message.MercadoPagoStatus))
                .TransitionTo(Processing),
            When(VerificarStatusRequested)
                .StartProcessing(nameof(VerificarStatusRequested))
                .Schedule(OperationTimeout,
                    ctx => new PagamentoOperationTimedOut(ctx.Saga.CorrelationId, nameof(VerificarStatusRequested)))
                .Publish(ctx => new ExecuteVerificarStatusPagamento(ctx.Saga.CorrelationId))
                .TransitionTo(Processing),
            // Eventos de finalização chegando no estado Initial são silenciosamente ignorados.
            Ignore(OperationCompleted),
            Ignore(OperationFailed),
            Ignore(OperationTimeout.Received)
        );

        During(Processing,
            Ignore(GerarPagamentoRequested),
            Ignore(MercadoPagoCallbackRecebidoRequested),
            Ignore(VerificarStatusRequested),
            When(OperationCompleted)
                .Unschedule(OperationTimeout)
                .Then(ctx =>
                {
                    ctx.Saga.OperacaoEmAndamento = null;
                    ctx.Saga.OperacaoIniciadaEm = null;
                    ctx.Saga.UltimoMotivoFalha = null;
                })
                .TransitionTo(Initial),
            When(OperationFailed)
                .Unschedule(OperationTimeout)
                .Then(ctx =>
                {
                    ctx.Saga.OperacaoEmAndamento = null;
                    ctx.Saga.OperacaoIniciadaEm = null;
                    ctx.Saga.UltimoMotivoFalha = ctx.Message.Motivo;
                })
                .TransitionTo(Initial),
            When(OperationTimeout.Received)
                .Then(ctx =>
                {
                    ctx.Saga.UltimoMotivoFalha =
                        $"Timeout após {OperationTimeoutDelay.TotalMinutes}min na operação '{ctx.Message.Operacao}'.";
                    ctx.Saga.OperacaoEmAndamento = null;
                    ctx.Saga.OperacaoIniciadaEm = null;
                })
                .TransitionTo(Initial)
        );
    }

    public State Processing { get; } = null!;

    public Event<GerarPagamentoRequest> GerarPagamentoRequested { get; } = null!;
    public Event<MercadoPagoCallbackRecebidoRequest> MercadoPagoCallbackRecebidoRequested { get; } = null!;
    public Event<VerificarStatusPagamentoRequest> VerificarStatusRequested { get; } = null!;

    public Event<PagamentoOperationCompleted> OperationCompleted { get; } = null!;
    public Event<PagamentoOperationFailed> OperationFailed { get; } = null!;

    public Schedule<PagamentoState, PagamentoOperationTimedOut> OperationTimeout { get; } = null!;

    private void ConfigureCorrelations()
    {
        Event(() => GerarPagamentoRequested, e => e.CorrelateById(ctx => ctx.Message.PagamentoId));
        Event(() => MercadoPagoCallbackRecebidoRequested, e => e.CorrelateById(ctx => ctx.Message.PagamentoId));
        Event(() => VerificarStatusRequested, e => e.CorrelateById(ctx => ctx.Message.PagamentoId));

        Event(() => OperationCompleted, e =>
        {
            e.CorrelateById(ctx => ctx.Message.PagamentoId);
            e.OnMissingInstance(m => m.Discard());
        });
        Event(() => OperationFailed, e =>
        {
            e.CorrelateById(ctx => ctx.Message.PagamentoId);
            e.OnMissingInstance(m => m.Discard());
        });
    }
}

[ExcludeFromCodeCoverage(Justification = "Helpers de DSL da saga — exercitados pela própria saga em integração.")]
internal static class PagamentoStateMachineExtensions
{
    public static EventActivityBinder<PagamentoState, T> StartProcessing<T>(
        this EventActivityBinder<PagamentoState, T> binder,
        string operacao)
        where T : class
    {
        return binder.Then(ctx =>
        {
            ctx.Saga.OperacaoEmAndamento = operacao;
            ctx.Saga.OperacaoIniciadaEm = DateTime.UtcNow;
        });
    }
}
