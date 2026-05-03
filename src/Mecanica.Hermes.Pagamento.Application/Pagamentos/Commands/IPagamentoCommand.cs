namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Commands;

/// <summary>
///     Marker interface para commands MediatR que operam sobre um <c>Pagamento</c> específico.
///     Usado pelo <c>TracingBehavior</c> para enriquecer o span com o id correlacionado.
/// </summary>
public interface IPagamentoCommand
{
    Guid PagamentoId { get; }
}
