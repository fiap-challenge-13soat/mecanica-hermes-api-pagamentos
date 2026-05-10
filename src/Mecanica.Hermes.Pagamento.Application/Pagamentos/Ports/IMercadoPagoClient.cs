namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

/// <summary>
///     Port (Hexagonal) que abstrai a interação com o Mercado Pago.
///     Suporta as duas operações usadas pelos consumers:
///     <list type="bullet">
///         <item>Criação de uma <c>preference</c> (link de pagamento);</item>
///         <item>Consulta de status do pagamento por <c>external_reference</c> (polling).</item>
///     </list>
/// </summary>
public interface IMercadoPagoClient
{
    /// <summary>
    ///     Cria uma preference no Mercado Pago para o pagamento especificado. O <c>externalReference</c>
    ///     deve ser o <c>PagamentoId</c> em string para correlação no webhook e no polling.
    /// </summary>
    Task<MercadoPagoCreatePreferenceResult> CreatePreferenceAsync(
        Guid pagamentoId,
        string emailCliente,
        string nomeCliente,
        decimal valor,
        string moeda,
        DateTime? vencimento,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Consulta o status atual do pagamento no Mercado Pago via search por
    ///     <c>external_reference</c>. Usado pelo polling agendado.
    /// </summary>
    Task<MercadoPagoPaymentStatus> GetPaymentStatusAsync(
        Guid pagamentoId,
        CancellationToken cancellationToken);
}

/// <summary>
///     Resultado da criação de uma preference no Mercado Pago.
/// </summary>
public sealed record MercadoPagoCreatePreferenceResult(
    string PreferenceId,
    string InitPoint);

/// <summary>
///     Status agregado do pagamento no Mercado Pago. Mapeia os estados que importam para
///     a SAGA local; o detalhe textual original é preservado em <see cref="DetalheOriginal" />
///     para diagnóstico.
/// </summary>
public sealed record MercadoPagoPaymentStatus(
    MercadoPagoPaymentOutcome Outcome,
    string DetalheOriginal);

public enum MercadoPagoPaymentOutcome
{
    /// <summary>Ainda não confirmado — o caller deve reagendar polling.</summary>
    Pending,

    /// <summary>Pagamento aprovado e capturado.</summary>
    Approved,

    /// <summary>Pagamento explicitamente recusado pelo gateway/banco.</summary>
    Rejected,

    /// <summary>Pagamento cancelado (pelo cliente ou pelo MP).</summary>
    Cancelled,

    /// <summary>Pagamento expirado no MP (preference vencida sem confirmação).</summary>
    Expired,

    /// <summary>Não encontrado — pagamento ainda não foi tentado pelo cliente.</summary>
    NotFound
}
