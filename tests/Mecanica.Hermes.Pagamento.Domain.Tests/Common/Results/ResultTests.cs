using System.Net;
using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Common.Results;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Common.Results;

public class ResultTests
{
    [Fact]
    public void Ok_ProduzSucesso()
    {
        var r = Result.Ok();
        r.IsSuccess.Should().BeTrue();
        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void BadRequest_AcumulaErros()
    {
        var r = Result.BadRequest(["a", "b"]);
        r.IsFailure.Should().BeTrue();
        r.Errors.Should().BeEquivalentTo("a", "b");
    }

    [Fact]
    public void GenericOk_CarregaData()
    {
        var r = Result<int>.Ok(42);
        r.Data.Should().Be(42);
        r.IsSuccess.Should().BeTrue();
    }
}
