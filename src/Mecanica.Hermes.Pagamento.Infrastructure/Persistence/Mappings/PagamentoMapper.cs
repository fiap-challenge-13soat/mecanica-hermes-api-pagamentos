using Mecanica.Hermes.Pagamento.Domain.Pagamentos;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.ValueObjects;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Mappings;

internal static class PagamentoMapper
{
    public static PagamentoDocument ToDocument(this PagamentoAggregate pagamento, int version)
    {
        return new PagamentoDocument
        {
            Id = pagamento.Id,
            OrdemDeServicoId = pagamento.OrdemDeServicoId,
            ClienteId = pagamento.ClienteId,
            EmailCliente = pagamento.EmailCliente.Valor,
            NomeCliente = pagamento.NomeCliente,
            Valor = pagamento.Valor.Valor,
            Moeda = pagamento.Valor.Moeda,
            Vencimento = pagamento.Vencimento,
            StatusAtual = pagamento.StatusAtual.StatusAtual.ToString(),
            StatusDestino = pagamento.StatusAtual.StatusDestino.ToString(),
            StatusInicio = pagamento.StatusAtual.DataInicio,
            StatusFinalizacao = pagamento.StatusAtual.DataFinalizacao,
            MercadoPagoPreferenceId = pagamento.LinkPagamento?.PreferenceId,
            LinkPagamentoUrl = pagamento.LinkPagamento?.Url,
            UltimoMotivoFalha = pagamento.UltimoMotivoFalha,
            ConfirmadoVia = pagamento.ConfirmadoVia?.ToString(),
            TentativasVerificacao = pagamento.TentativasVerificacao,
            LimiteVerificacoes = pagamento.LimiteVerificacoes,
            CreatedAt = pagamento.CreatedAt,
            UpdatedAt = pagamento.UpdatedAt,
            Version = version,
            HistoricosStatus = pagamento.HistoricosStatus
                .Select(h => new PagamentoHistoricoStatusDocument
                {
                    Id = h.Id == Guid.Empty ? Guid.NewGuid() : h.Id,
                    StatusAnterior = h.StatusAnterior.ToString(),
                    StatusAtual = h.StatusAtual.ToString(),
                    StatusDestino = h.StatusDestino.ToString(),
                    DataInicio = h.DataInicio,
                    DataFinalizacao = h.DataFinalizacao,
                    CreatedAt = h.CreatedAt,
                    UpdatedAt = h.UpdatedAt
                })
                .ToList()
        };
    }

    public static PagamentoAggregate ToDomain(this PagamentoDocument document)
    {
        var statusAtualEnum = Enum.Parse<PagamentoStatus>(document.StatusAtual);
        var statusDestinoEnum = string.IsNullOrEmpty(document.StatusDestino)
            ? statusAtualEnum
            : Enum.Parse<PagamentoStatus>(document.StatusDestino);

        var statusAtual = CriarStatusInstancia(statusAtualEnum);
        statusAtual.GetType();
        // Restaura via método interno do StatusBase
        typeof(PagamentoStatusBase)
            .GetMethod("Restaurar",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(statusAtual, [
                Guid.NewGuid(),
                statusDestinoEnum,
                document.StatusInicio,
                document.StatusFinalizacao,
                document.CreatedAt,
                document.UpdatedAt
            ]);

        var emailResult = EmailCliente.Criar(document.EmailCliente);
        var valorResult = Dinheiro.Criar(document.Valor, document.Moeda);

        var link = document.MercadoPagoPreferenceId is not null && document.LinkPagamentoUrl is not null
            ? LinkPagamento.Criar(document.MercadoPagoPreferenceId, document.LinkPagamentoUrl).Data
            : null;

        var historicos = document.HistoricosStatus
            .Select(h => PagamentoHistoricoStatus.Restaurar(
                h.Id,
                Enum.Parse<PagamentoStatus>(h.StatusAnterior),
                Enum.Parse<PagamentoStatus>(h.StatusAtual),
                Enum.Parse<PagamentoStatus>(h.StatusDestino),
                h.DataInicio,
                h.DataFinalizacao,
                h.CreatedAt,
                h.UpdatedAt))
            .ToList();

        PagamentoConfirmadoVia? confirmadoVia = string.IsNullOrEmpty(document.ConfirmadoVia)
            ? null
            : Enum.Parse<PagamentoConfirmadoVia>(document.ConfirmadoVia);

        return PagamentoAggregate.Restaurar(
            document.Id,
            document.OrdemDeServicoId,
            document.ClienteId,
            emailResult.Data!,
            document.NomeCliente,
            valorResult.Data!,
            document.Vencimento,
            statusAtual,
            link,
            document.UltimoMotivoFalha,
            confirmadoVia,
            document.TentativasVerificacao,
            document.LimiteVerificacoes,
            historicos,
            document.CreatedAt,
            document.UpdatedAt);
    }

    private static PagamentoStatusBase CriarStatusInstancia(PagamentoStatus status)
    {
        return status switch
        {
            PagamentoStatus.PendenteGeracao => new PagamentoPendenteGeracao(),
            PagamentoStatus.LinkGerado => new PagamentoLinkGerado(),
            PagamentoStatus.AguardandoConfirmacao => new PagamentoAguardandoConfirmacao(),
            PagamentoStatus.Pago => new PagamentoPago(),
            PagamentoStatus.Recusado => new PagamentoRecusado(),
            PagamentoStatus.Expirado => new PagamentoExpirado(),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Status desconhecido.")
        };
    }
}
