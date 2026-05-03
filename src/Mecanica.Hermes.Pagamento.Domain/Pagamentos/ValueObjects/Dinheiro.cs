using Mecanica.Hermes.Pagamento.Domain.Common.Results;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.ValueObjects;

/// <summary>
///     Valor monetário com moeda fixa em BRL (Real Brasileiro). Limite: 2 casas decimais
///     (centavos), arredondamento para baixo (truncamento). Valor mínimo: 0,01.
/// </summary>
public sealed class Dinheiro
{
    public const string MoedaPadrao = "BRL";

    public Dinheiro()
    {
        // Constructor para o driver Mongo (deserialização)
        Moeda = MoedaPadrao;
    }

    private Dinheiro(decimal valor, string moeda)
    {
        Valor = valor;
        Moeda = moeda;
    }

    public decimal Valor { get; private set; }
    public string Moeda { get; private set; }

    public static Result<Dinheiro> Criar(decimal valor, string moeda = MoedaPadrao)
    {
        if (string.IsNullOrWhiteSpace(moeda))
            return Result<Dinheiro>.BadRequest("A moeda é obrigatória.");

        if (valor < 0.01m)
            return Result<Dinheiro>.BadRequest("O valor do pagamento deve ser maior ou igual a 0,01.");

        var truncado = Math.Floor(valor * 100) / 100;
        return Result<Dinheiro>.Ok(new Dinheiro(truncado, moeda.ToUpperInvariant()));
    }
}
