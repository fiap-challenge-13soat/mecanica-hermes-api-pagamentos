using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.DependencyInjection;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Mappings;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using MongoDB.Bson;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.Persistence;

/// <summary>
///     Contract test: o `ConventionPack` desta API garante que toda key Bson do `PagamentoDocument`
///     seja persistida em camelCase (vide <c>ServiceCollectionExtensions.EnsureConventions</c>).
///     Quem consultar o Mongo de fora — outro serviço, helper de teste E2E, ferramenta de
///     observabilidade — pode confiar que `ordemDeServicoId`, `statusAtual`, etc. são
///     os nomes corretos. Se alguém remover a convention pack ou adicionar campos com
///     <c>[BsonElement("PascalCase")]</c>, este teste falha.
/// </summary>
public class MongoCamelCaseConventionTests
{
    private static readonly string[] AllowedExceptions =
    [
        "_id" // identificador padrão do MongoDB; não-camelCase por convenção do driver
    ];

    [Fact]
    public void Pagamento_SerializadoEmBson_TemTodasKeysEmCamelCase()
    {
        ServiceCollectionExtensions.EnsureConventions();
        var document = new PagamentoBuilder().BuildAguardandoConfirmacao().ToDocument(version: 1);

        var bson = document.ToBsonDocument();

        var violacoes = CollectKeyViolations(bson, path: "");
        violacoes.Should().BeEmpty(
            "todas as keys do PagamentoDocument devem ser camelCase (ou estar em AllowedExceptions). " +
            $"Violações: {string.Join("; ", violacoes)}");
    }

    private static List<string> CollectKeyViolations(BsonValue value, string path)
    {
        var violacoes = new List<string>();
        switch (value)
        {
            case BsonDocument doc:
                foreach (var element in doc.Elements)
                {
                    if (!IsAllowed(element.Name))
                        violacoes.Add($"{path}/{element.Name}");
                    violacoes.AddRange(CollectKeyViolations(element.Value, $"{path}/{element.Name}"));
                }
                break;
            case BsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                    violacoes.AddRange(CollectKeyViolations(arr[i], $"{path}[{i}]"));
                break;
        }
        return violacoes;
    }

    private static bool IsAllowed(string key)
    {
        if (AllowedExceptions.Contains(key)) return true;
        // camelCase: começa com letra minúscula ASCII.
        return key.Length > 0 && key[0] is >= 'a' and <= 'z';
    }
}
