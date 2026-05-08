using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Microsoft.Extensions.Logging;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Filters;

[ExcludeFromCodeCoverage(Justification = "Filtro MassTransit consume pipeline — exercitado pelos consumers em integração.")]
internal sealed partial class PagamentoCommandValidationFilter<T>(
    ILogger<PagamentoCommandValidationFilter<T>> logger)
    : IFilter<ConsumeContext<T>>
    where T : class, IPagamentoExecuteCommand
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (context.Message.PagamentoId == Guid.Empty)
        {
            LogPagamentoIdVazio(logger, typeof(T).Name);
            return;
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("pagamentoCommandValidation");
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Mensagem {MessageType} recebida com PagamentoId vazio. Mensagem descartada.")]
    private static partial void LogPagamentoIdVazio(ILogger logger, string messageType);
}
