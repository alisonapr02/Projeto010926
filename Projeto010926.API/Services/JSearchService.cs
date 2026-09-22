using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Projeto010926.API.Services;

public sealed record VagaExterna(string? Id, string? Titulo, string? Empresa, string? Cidade,
    string? Estado, string? Localizacao, string? Descricao, string? LinkCandidatura,
    string? PublicadaEm, bool? Remota, string? Salario, string? SiteOrigem);
public sealed record ResultadoExterno(string Fonte, string Consulta, string? ProximoCursor, int Quantidade,
    DateTimeOffset ConsultadoEm, VagaExterna[] Itens);
public sealed class JSearchException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed class JSearchService(IHttpClientFactory factory, IConfiguration configuration, IMemoryCache cache)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ResultadoExterno> Buscar(string? termo, string? cidade, string? estado, string? cursor, CancellationToken ct)
    {
        var chave = configuration["JSearch:ApiKey"];
        if (string.IsNullOrWhiteSpace(chave))
            throw new JSearchException(503, "Configure a chave JSearch no servidor para buscar vagas externas.");
        var termoBusca = string.IsNullOrWhiteSpace(termo) ? "vagas" : termo.Trim();
        var consulta = string.IsNullOrWhiteSpace(cidade) || string.IsNullOrWhiteSpace(estado)
            ? termoBusca : $"{termoBusca} em {cidade.Trim()}, {estado.ToUpperInvariant()}, Brasil";
        var cacheKey = $"jsearch-v2:{consulta.ToLowerInvariant()}:{cursor}";
        await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue<ResultadoExterno>(cacheKey, out var salvo)) return salvo!;
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"search-v2?query={Uri.EscapeDataString(consulta)}&country=br&language=pt" +
                (string.IsNullOrWhiteSpace(cursor) ? "" : $"&cursor={Uri.EscapeDataString(cursor)}"));
            request.Headers.Add("x-api-key", chave);
            using var client = factory.CreateClient("JSearch");
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new JSearchException((int)response.StatusCode == 429 ? 429 : 502,
                    (int)response.StatusCode switch
                    {
                        401 => "A JSearch recusou a chave configurada. Confira a chave no painel do fornecedor.",
                        403 => "Acesso recusado pela JSearch. Confira a assinatura do plano e as permissões no painel.",
                        429 => "Limite de consultas da JSearch atingido. Tente após a renovação da cota.",
                        _ => "O serviço de vagas está indisponível. Tente novamente mais tarde."
                    });
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            if (Texto(root, "status") != "OK" || !root.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
                throw new JSearchException(502, "A JSearch retornou uma resposta inesperada.");
            var itens = jobs.EnumerateArray().Select(j => new VagaExterna(
                Texto(j, "job_id"), Texto(j, "job_title"), Texto(j, "employer_name"), Texto(j, "job_city"),
                Texto(j, "job_state"), Texto(j, "job_location"), Texto(j, "job_description"),
                Link(Texto(j, "job_apply_link")), Texto(j, "job_posted_at_datetime_utc"),
                j.TryGetProperty("job_is_remote", out var remoto) && remoto.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? remoto.GetBoolean() : null,
                Texto(j, "job_salary_string"), Texto(j, "job_publisher"))).ToArray();
            var resultado = new ResultadoExterno("JSearch", consulta, Texto(data, "cursor"), itens.Length, DateTimeOffset.UtcNow, itens);
            cache.Set(cacheKey, resultado, TimeSpan.FromMinutes(30));
            return resultado;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new JSearchException(504, "A busca externa demorou demais. Tente novamente."); }
        catch (HttpRequestException)
        { throw new JSearchException(502, "Não foi possível conectar ao serviço de vagas."); }
        catch (JsonException)
        { throw new JSearchException(502, "O serviço de vagas retornou dados inválidos."); }
        finally { gate.Release(); }
    }

    private static string? Texto(JsonElement item, string nome) =>
        item.TryGetProperty(nome, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string? Link(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? value : null;
}
