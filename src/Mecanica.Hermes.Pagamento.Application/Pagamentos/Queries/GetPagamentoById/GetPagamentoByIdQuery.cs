using Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;
using Mecanica.Hermes.Shared.Core.Results;
using MediatR;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Queries.GetPagamentoById;

public sealed record GetPagamentoByIdQuery(Guid Id) : IRequest<Result<PagamentoDto>>;
