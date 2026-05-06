using Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

public interface IClienteApiClient
{
    Task<ClienteDto?> GetByIdAsync(Guid clienteId, CancellationToken ct = default);
}
