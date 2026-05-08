using Mecanica.Hermes.Shared.Core.Pagination;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

/// <summary>
///     Port (Hexagonal) de leitura para o agregado <see cref="Domain.Pagamentos.Pagamento" />.
///     Aplicação do princípio ISP: Query Handlers não devem conhecer mutações de escrita.
/// </summary>
public interface IPagamentoReader
{
    Task<Domain.Pagamentos.Pagamento?> GetByIdAsync(Guid id);
    Task<Domain.Pagamentos.Pagamento?> GetByOrdemDeServicoIdAsync(Guid ordemDeServicoId);
    Task<PagedResult<Domain.Pagamentos.Pagamento>> ListByStatusAsync(
        PagamentoStatus status,
        PaginationParams paginationParams);
    Task<PagedResult<Domain.Pagamentos.Pagamento>> ListAllAsync(PaginationParams paginationParams);
}
