// IMPORTANTE — esta classe vive de propósito no namespace
// `Mecanica.Hermes.Application.OrdensDeServico.Messages.Requests` (sem o ".Pagamento."), igual
// ao namespace usado pela API de Ordem de Serviço. O MassTransit gera a `MessageUrn` a partir
// do *namespace* + nome do tipo; alinhar os dois lados garante que a mensagem publicada por
// nós seja roteada para a SAGA da OS sem precisar de mapeamento custom de tipo.
//
// NÃO mova esta classe para outro namespace antes de extrair a lib de contratos
// `Mecanica.Hermes.Contracts.OrdemDeServico` (ADR-017 da OS) — se for movida, a API de OS
// para de receber a confirmação de pagamento.

namespace Mecanica.Hermes.Application.OrdensDeServico.Messages.Requests;

/// <summary>
///     Contrato cross-service: avisa a API de Ordem de Serviço que o pagamento foi aprovado.
///     A SAGA da OS está correlacionada por <see cref="OrdemDeServicoId" /> e responde com a
///     transição <c>AguardandoPagamento → PagamentoConfirmado</c>.
/// </summary>
public sealed record PagamentoAprovadoRequest(Guid OrdemDeServicoId);
