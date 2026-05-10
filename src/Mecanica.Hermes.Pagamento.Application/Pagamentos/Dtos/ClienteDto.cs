namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;

public sealed record ClienteDto(Guid Id, string Email, string NomeCivil, string? NomeSocial)
{
    public string NomeExibicao => NomeSocial ?? NomeCivil;
}
