namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;

/// <summary>
///     Marca uma mensagem como uma requisição correlacionada por <see cref="PagamentoId" />.
///     Cada request entra na SAGA <c>PagamentoStateMachine</c> e dispara, no máximo, uma
///     operação ativa por <see cref="PagamentoId" />.
/// </summary>
public interface IPagamentoRequest
{
    Guid PagamentoId { get; }
}
