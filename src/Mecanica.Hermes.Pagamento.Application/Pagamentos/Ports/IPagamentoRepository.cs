namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

/// <summary>
///     Composite port que une <see cref="IPagamentoReader" /> e <see cref="IPagamentoWriter" />.
/// </summary>
public interface IPagamentoRepository : IPagamentoReader, IPagamentoWriter
{
}
