using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;

public interface IPagamentoStatus
{
    PagamentoStatus StatusAtual { get; }
    PagamentoStatus StatusDestino { get; }
    DateTime DataInicio { get; }
    DateTime? DataFinalizacao { get; }
}
