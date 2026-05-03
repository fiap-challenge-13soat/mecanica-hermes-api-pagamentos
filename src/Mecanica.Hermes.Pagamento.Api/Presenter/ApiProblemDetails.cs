using Microsoft.AspNetCore.Mvc;

namespace Mecanica.Hermes.Pagamento.Api.Presenter;

public sealed class ApiProblemDetails : ProblemDetails
{
    public List<string> Errors { get; init; } = [];
}
