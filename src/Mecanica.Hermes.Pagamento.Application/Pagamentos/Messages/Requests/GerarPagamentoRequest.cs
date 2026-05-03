namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;

/// <summary>
///     Solicita à SAGA a criação de um novo pagamento (geração de link no Mercado Pago).
///     O <see cref="PagamentoId" /> é gerado pelo cliente (geralmente a camada API) antes
///     do envio para que possa ser usado em <c>GET /api/pagamentos/{id}</c> imediatamente.
/// </summary>
public sealed record GerarPagamentoRequest(
    Guid PagamentoId,
    Guid OrdemDeServicoId,
    Guid ClienteId,
    string EmailCliente,
    string NomeCliente,
    decimal Valor,
    DateTime? Vencimento) : IPagamentoRequest;
