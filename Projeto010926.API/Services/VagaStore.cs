using System.Text.Json;
using Projeto010926.API.Models;

namespace Projeto010926.API.Services;

// Armazenamento para uma instância local da aplicação.
public sealed class VagaStore
{
    private readonly object gate = new();
    private readonly string arquivo;
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private List<Vaga> vagas;

    public VagaStore(IWebHostEnvironment environment, IConfiguration configuration)
    {
        arquivo = Path.GetFullPath(configuration["Vagas:Arquivo"] ?? "App_Data/vagas.json", environment.ContentRootPath);
        vagas = File.Exists(arquivo)
            ? JsonSerializer.Deserialize<List<Vaga>>(File.ReadAllText(arquivo), json)
                ?? throw new InvalidDataException("Arquivo de vagas inválido.")
            : [];
    }

    public Vaga[] Listar() { lock (gate) return vagas.ToArray(); }

    public Vaga Criar(CadastroVaga dados)
    {
        lock (gate)
        {
            var vaga = new Vaga(Guid.NewGuid(), dados with
            {
                Titulo = dados.Titulo.Trim(), Empresa = dados.Empresa.Trim(),
                Descricao = dados.Descricao.Trim(), Cidade = dados.Cidade.Trim(),
                Estado = dados.Estado.ToUpperInvariant(), LinkCandidatura = dados.LinkCandidatura.Trim()
            }, DateTimeOffset.UtcNow, true);
            Salvar([.. vagas, vaga]);
            return vaga;
        }
    }

    public bool Encerrar(Guid id)
    {
        lock (gate)
        {
            if (!vagas.Any(v => v.Id == id)) return false;
            Salvar(vagas.Select(v => v.Id == id ? v with { Ativa = false } : v).ToList());
            return true;
        }
    }

    private void Salvar(List<Vaga> novas)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
        var temporario = arquivo + ".tmp";
        File.WriteAllText(temporario, JsonSerializer.Serialize(novas, json));
        File.Move(temporario, arquivo, overwrite: true);
        vagas = novas;
    }
}
