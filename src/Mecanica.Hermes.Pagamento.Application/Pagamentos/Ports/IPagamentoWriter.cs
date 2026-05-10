namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

/// <summary>
///     Port (Hexagonal) de escrita para o agregado <see cref="Domain.Pagamentos.Pagamento" />.
/// </summary>
public interface IPagamentoWriter
{
    Task AddAsync(Domain.Pagamentos.Pagamento pagamento);
    Task UpdateAsync(Domain.Pagamentos.Pagamento pagamento);
}
