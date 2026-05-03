using Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Mappings;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Common.Results;
using MediatR;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Queries.GetPagamentoById;

internal sealed class GetPagamentoByIdQueryHandler(IPagamentoReader repository)
    : IRequestHandler<GetPagamentoByIdQuery, Result<PagamentoDto>>
{
    public async Task<Result<PagamentoDto>> Handle(
        GetPagamentoByIdQuery request,
        CancellationToken cancellationToken)
    {
        var pagamento = await repository.GetByIdAsync(request.Id);

        if (pagamento is null)
            return Result<PagamentoDto>.NotFound();

        return Result<PagamentoDto>.Ok(pagamento.ToDto());
    }
}
