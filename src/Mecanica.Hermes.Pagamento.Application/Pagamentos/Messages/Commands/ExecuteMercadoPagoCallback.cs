namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;

/// <summary>
///     Comando publicado pela SAGA quando recebe um <c>MercadoPagoCallbackRecebidoRequest</c>.
///     Carrega o status textual do MP para que o
///     <c>MercadoPagoCallbackConsumer</c> roteie internamente para confirmar/recusar.
/// </summary>
public sealed record ExecuteMercadoPagoCallback(
    Guid PagamentoId,
    string MercadoPagoStatus) : IPagamentoExecuteCommand;
