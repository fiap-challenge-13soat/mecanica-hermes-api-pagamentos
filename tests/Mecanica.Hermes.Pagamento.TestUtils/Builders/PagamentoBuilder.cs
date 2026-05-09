using Bogus;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.TestUtils.Builders;

/// <summary>
///     Builder de testes que cria <see cref="Pagamento" /> com defaults plausíveis via Bogus.
///     Permite override fluente apenas dos campos relevantes para o teste.
/// </summary>
public sealed class PagamentoBuilder
{
    public const string DefaultPreferenceId = "TEST-PREF";
    public const string DefaultPagamentoUrl = "https://mp.example/pay";

    private static readonly Faker _faker = new("pt_BR");

    private Guid _id = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private Guid _clienteId = Guid.NewGuid();
    private string _email = _faker.Internet.Email();
    private string _nome = _faker.Person.FullName;
    private decimal _valor = Math.Round(_faker.Random.Decimal(50, 5000), 2);
    private DateTime? _vencimento = DateTime.UtcNow.AddDays(7);
    private int _limiteVerificacoes = PagamentoAggregate.LimiteVerificacoesDefault;

    public PagamentoBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public PagamentoBuilder WithOrdemDeServicoId(Guid ordemId)
    {
        _ordemDeServicoId = ordemId;
        return this;
    }

    public PagamentoBuilder WithClienteId(Guid clienteId)
    {
        _clienteId = clienteId;
        return this;
    }

    public PagamentoBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public PagamentoBuilder WithNome(string nome)
    {
        _nome = nome;
        return this;
    }

    public PagamentoBuilder WithValor(decimal valor)
    {
        _valor = valor;
        return this;
    }

    public PagamentoBuilder WithVencimento(DateTime? vencimento)
    {
        _vencimento = vencimento;
        return this;
    }

    public PagamentoBuilder WithLimiteVerificacoes(int limite)
    {
        _limiteVerificacoes = limite;
        return this;
    }

    public PagamentoAggregate Build()
    {
        var result = PagamentoAggregate.Criar(
            _id,
            _ordemDeServicoId,
            _clienteId,
            _email,
            _nome,
            _valor,
            _vencimento,
            _limiteVerificacoes);

        if (result.IsFailure)
            throw new InvalidOperationException(
                "PagamentoBuilder produziu um pagamento inválido: " +
                string.Join("; ", result.Errors));

        return result.Data!;
    }

    /// <summary>Atalho: cria um pagamento já com link gerado (status AguardandoConfirmacao).</summary>
    public PagamentoAggregate BuildAguardandoConfirmacao(
        string preferenceId = DefaultPreferenceId,
        string url = DefaultPagamentoUrl)
    {
        var pagamento = Build();
        var ger = pagamento.GerarLink(preferenceId, url);
        if (ger.IsFailure)
            throw new InvalidOperationException("Falha ao gerar link: " + string.Join("; ", ger.Errors));

        var marcar = pagamento.MarcarEmailEnviado();
        if (marcar.IsFailure)
            throw new InvalidOperationException(
                "Falha ao marcar e-mail enviado: " + string.Join("; ", marcar.Errors));

        return pagamento;
    }
}
