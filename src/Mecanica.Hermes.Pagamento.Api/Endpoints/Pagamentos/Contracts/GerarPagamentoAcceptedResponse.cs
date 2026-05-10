namespace Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Contracts;

/// <summary>
///     Resposta retornada para a criação assíncrona de um pagamento. O <see cref="Id" /> é
///     gerado pela API antes da mensagem ser enfileirada, permitindo que o cliente consulte o
///     pagamento em <c>GET /api/pagamentos/{id}</c> assim que o consumer terminar a persistência.
/// </summary>
public sealed record GerarPagamentoAcceptedResponse(Guid Id);
