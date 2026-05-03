using Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;
using Mecanica.Hermes.Pagamento.Domain.Common.Results;
using MediatR;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Queries.GetPagamentoById;

public sealed record GetPagamentoByIdQuery(Guid Id) : IRequest<Result<PagamentoDto>>;
