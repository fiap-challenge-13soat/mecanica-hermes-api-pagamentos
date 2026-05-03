namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;

/// <summary>
///     Comando enfileirado para a lambda de e-mail (futura) consumir e enviar o link de
///     pagamento ao cliente via AWS SES. Esta API apenas publica — o consumer é externo.
/// </summary>
public sealed record EnviarEmailPagamentoCommand(
    Guid PagamentoId,
    Guid OrdemDeServicoId,
    string EmailCliente,
    string NomeCliente,
    string LinkPagamento,
    decimal Valor,
    string Moeda,
    DateTime? Vencimento);
