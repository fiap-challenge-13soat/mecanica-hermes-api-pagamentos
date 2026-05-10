namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;

/// <summary>
///     Mensagem agendada (delayed) pela SAGA quando o pagamento entra em
///     <c>AguardandoConfirmacao</c>. Dispara um polling no Mercado Pago para
///     reconciliar caso o webhook não chegue. A SAGA reagenda enquanto
///     <c>TentativasVerificacao &lt; LimiteVerificacoes</c>.
/// </summary>
public sealed record VerificarStatusPagamentoRequest(Guid PagamentoId) : IPagamentoRequest;
