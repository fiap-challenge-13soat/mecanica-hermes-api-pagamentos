using FluentAssertions;
using Mecanica.Hermes.Pagamento.Application.Common.Pagination;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Common.Pagination;

public class PaginationParamsTests
{
    [Fact]
    public void Construtor_Default_DefinePagina1ETamanho10()
    {
        var p = new PaginationParams();
        p.Page.Should().Be(1);
        p.PageSize.Should().Be(10);
        p.Skip.Should().Be(0);
    }

    [Fact]
    public void Page_MenorQue1_NormalizaPara1()
    {
        var p = new PaginationParams { Page = 0 };
        p.Page.Should().Be(1);
    }

    [Fact]
    public void PageSize_MenorQue1_NormalizaParaDefault()
    {
        var p = new PaginationParams { PageSize = 0 };
        p.PageSize.Should().Be(10);
    }

    [Fact]
    public void PageSize_MaiorQueMaximo_NormalizaParaMaximo()
    {
        var p = new PaginationParams { PageSize = 1000 };
        p.PageSize.Should().Be(100);
    }

    [Fact]
    public void PageSize_DentroDoLimite_AceitaValor()
    {
        var p = new PaginationParams { PageSize = 25 };
        p.PageSize.Should().Be(25);
    }

    [Fact]
    public void Skip_PaginaMaiorQueUm_RetornaCalculoCorreto()
    {
        var p = new PaginationParams { Page = 4, PageSize = 20 };
        p.Skip.Should().Be(60);
    }
}

public class PagedResultTests
{
    [Fact]
    public void Create_CalculaPropriedades()
    {
        var items = new[] { 1, 2, 3 };
        var result = PagedResult<int>.Create(items, page: 2, pageSize: 3, totalCount: 7);

        result.Items.Should().BeEquivalentTo(items);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(3);
        result.TotalCount.Should().Be(7);
        result.TotalPages.Should().Be(3); // ceil(7/3) = 3
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Create_PrimeiraPagina_NaoTemPaginaAnterior()
    {
        var result = PagedResult<int>.Create(new[] { 1, 2 }, 1, 2, 2);

        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeFalse();
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public void Create_UltimaPagina_NaoTemProxima()
    {
        var result = PagedResult<int>.Create(new[] { 1 }, 3, 2, 5);

        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeTrue();
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public void Create_ColecaoVazia_RetornaItemsVazios()
    {
        var result = PagedResult<int>.Create([], 1, 10, 0);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }
}
