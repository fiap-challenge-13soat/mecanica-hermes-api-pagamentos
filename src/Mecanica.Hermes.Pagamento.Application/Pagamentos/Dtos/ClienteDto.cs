namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;

public sealed record ClienteDto(Guid Id, string Email, string NomeCivil, string? NomeSocial)
{
    // Cadastros pode retornar nomeSocial como string vazia (nao null) quando o campo
    // nao foi preenchido. NomeSocial ?? NomeCivil retornaria "" nesse caso, fazendo
    // Pagamento.Criar falhar em string.IsNullOrWhiteSpace(nomeCliente).
    public string NomeExibicao =>
        string.IsNullOrWhiteSpace(NomeSocial) ? NomeCivil : NomeSocial;
}
