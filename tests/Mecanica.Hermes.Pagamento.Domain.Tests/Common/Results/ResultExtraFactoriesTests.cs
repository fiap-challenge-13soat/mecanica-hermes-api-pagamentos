using System.Net;
using FluentAssertions;
using Mecanica.Hermes.Shared.Core.Results;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Common.Results;

public class ResultExtraFactoriesTests
{
    [Fact]
    public void Accepted_ProduzSucessoCom202()
    {
        var r = Result.Accepted();

        r.IsSuccess.Should().BeTrue();
        r.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public void NotFound_NaoProduzSucesso()
    {
        var r = Result.NotFound();

        r.IsFailure.Should().BeTrue();
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void BadRequest_ComMensagemUnica_AcumulaErroUnico()
    {
        var r = Result.BadRequest("falha");

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().BeEquivalentTo("falha");
    }

    [Fact]
    public void Errors_DefaultEhListaVazia()
    {
        var r = Result.Ok();

        r.Errors.Should().BeEmpty();
    }

    [Fact]
    public void IsSuccess_ConsideraStatusEntre200E202()
    {
        new Result(HttpStatusCode.OK).IsSuccess.Should().BeTrue();
        new Result(HttpStatusCode.Created).IsSuccess.Should().BeTrue();
        new Result(HttpStatusCode.Accepted).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void IsFailure_ConsideraQualquerErroAcumulado()
    {
        var r = new Result(HttpStatusCode.OK);
        r.Errors.Add("erro");

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GenericOk_SemValor_RetornaDataDefault()
    {
        var r = Result<int>.Ok();

        r.IsSuccess.Should().BeTrue();
        r.Data.Should().Be(default);
    }

    [Fact]
    public void GenericForbidden_RetornaForbidden()
    {
        var r = Result<int>.Forbidden();

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GenericUnauthorized_RetornaUnauthorized()
    {
        var r = Result<int>.Unauthorized();

        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GenericNoContent_RetornaNoContent()
    {
        var r = Result<int>.NoContent();

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        r.IsFailure.Should().BeTrue(); // 204 não está no range 200-202
    }

    [Fact]
    public void GenericNotFound_RetornaNotFound()
    {
        var r = Result<string>.NotFound();

        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GenericCreated_RetornaCreatedComData()
    {
        var r = Result<int>.Created(42);

        r.StatusCode.Should().Be(HttpStatusCode.Created);
        r.IsSuccess.Should().BeTrue();
        r.Data.Should().Be(42);
    }

    [Fact]
    public void GenericConflict_RetornaConflictComErro()
    {
        var r = Result<int>.Conflict("conflito detectado");

        r.StatusCode.Should().Be(HttpStatusCode.Conflict);
        r.IsFailure.Should().BeTrue();
        r.Errors.Should().Contain("conflito detectado");
    }

    [Fact]
    public void GenericBadRequest_ComMensagemUnica_AcumulaErro()
    {
        var r = Result<int>.BadRequest("falha");

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().BeEquivalentTo("falha");
    }

    [Fact]
    public void GenericBadRequest_ComMultiplasMensagens_AcumulaTodos()
    {
        var r = Result<int>.BadRequest(["a", "b", "c"]);

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().BeEquivalentTo("a", "b", "c");
    }

    [Fact]
    public void GenericInternalServerError_RetornaErroComMensagem()
    {
        var r = Result<int>.InternalServerError("explosão");

        r.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        r.IsFailure.Should().BeTrue();
        r.Errors.Should().Contain("explosão");
    }

    [Fact]
    public void GenericConstrutor_AceitaValorComStatusOk()
    {
        var r = new Result<string>(HttpStatusCode.OK, "abc");

        r.IsSuccess.Should().BeTrue();
        r.Data.Should().Be("abc");
    }

    [Fact]
    public void GenericConstrutor_SemValor_DataEhDefault()
    {
        var r = new Result<string>(HttpStatusCode.OK);

        r.Data.Should().BeNull();
    }
}
