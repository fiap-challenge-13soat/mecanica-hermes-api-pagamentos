using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;

/// <summary>
///     Documento Mongo da coleção <c>pagamentos</c>. Mapeia diretamente para BSON via
///     atributos do driver — separado do agregado de domínio, sem nenhum vazamento.
/// </summary>
internal sealed class PagamentoDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid OrdemDeServicoId { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid ClienteId { get; set; }

    public string EmailCliente { get; set; } = string.Empty;
    public string NomeCliente { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Moeda { get; set; } = "BRL";
    public DateTime? Vencimento { get; set; }

    public string StatusAtual { get; set; } = string.Empty;
    public string StatusDestino { get; set; } = string.Empty;
    public DateTime StatusInicio { get; set; }
    public DateTime? StatusFinalizacao { get; set; }

    public string? MercadoPagoPreferenceId { get; set; }
    public string? LinkPagamentoUrl { get; set; }

    public string? UltimoMotivoFalha { get; set; }
    public string? ConfirmadoVia { get; set; }

    public int TentativasVerificacao { get; set; }
    public int LimiteVerificacoes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token (incrementado a cada update).</summary>
    [BsonElement("version")]
    public int Version { get; set; }

    public List<PagamentoHistoricoStatusDocument> HistoricosStatus { get; set; } = [];
}

internal sealed class PagamentoHistoricoStatusDocument
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string StatusAnterior { get; set; } = string.Empty;
    public string StatusAtual { get; set; } = string.Empty;
    public string StatusDestino { get; set; } = string.Empty;
    public DateTime DataInicio { get; set; }
    public DateTime? DataFinalizacao { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
