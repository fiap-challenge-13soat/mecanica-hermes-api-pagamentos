using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

namespace Mecanica.Hermes.Pagamento.Infrastructure.HttpClients;

internal sealed class ClienteApiClient(HttpClient httpClient) : IClienteApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ClienteDto?> GetByIdAsync(Guid clienteId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/clientes/{clienteId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<ClienteDto>(json, JsonOptions);
    }
}
