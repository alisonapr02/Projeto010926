using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Projeto010926.API.Models;

namespace Projeto010926.API.Services;

public sealed class ContaStore
{
    private readonly object gate = new();
    private readonly string arquivo;
    private readonly PasswordHasher<string> hasher = new();
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private List<Conta> contas;
    private readonly string dummyHash;

    public ContaStore(IWebHostEnvironment env, IConfiguration config)
    {
        arquivo = Path.GetFullPath(config["Contas:Arquivo"] ?? "App_Data/contas.json", env.ContentRootPath);
        contas = File.Exists(arquivo) ? JsonSerializer.Deserialize<List<Conta>>(File.ReadAllText(arquivo), json)
            ?? throw new InvalidDataException("Base de contas inválida.") : [];
        dummyHash = hasher.HashPassword("", Guid.NewGuid().ToString());
    }
    public Conta? Obter(Guid id) { lock (gate) return contas.Find(c => c.Id == id); }
    public IReadOnlyList<Conta> Listar() { lock (gate) return contas.ToArray(); }
    public Conta? Criar(CadastroConta dados)
    {
        var email = dados.Email.Trim().ToLowerInvariant();
        var hash = hasher.HashPassword(email, dados.Senha);
        lock (gate)
        {
            if (contas.Any(c => c.Email == email)) return null;
            var conta = new Conta(Guid.NewGuid(), dados.Nome.Trim(), email, hash, "João Pessoa", DateTimeOffset.UtcNow, []);
            Gravar([.. contas, conta]); return conta;
        }
    }
    public Conta? Autenticar(LoginConta dados)
    {
        Conta? conta;
        var email = dados.Email.Trim().ToLowerInvariant();
        lock (gate) conta = contas.Find(c => c.Email == email);
        var resultado = hasher.VerifyHashedPassword(email, conta?.SenhaHash ?? dummyHash, dados.Senha);
        return conta != null && resultado != PasswordVerificationResult.Failed ? conta : null;
    }
    public Conta? CriarAdministrativo(CriarContaAdministrativa dados)
    {
        var email = dados.Email.Trim().ToLowerInvariant();
        var hash = hasher.HashPassword(email, dados.Senha);
        lock (gate)
        {
            if (contas.Any(c => c.Email == email)) return null;
            var conta = new Conta(Guid.NewGuid(), dados.Nome.Trim(), email, hash, dados.Cidade.Trim(), DateTimeOffset.UtcNow, [], dados.Perfil);
            Gravar([.. contas, conta]); return conta;
        }
    }
    public Conta? AtualizarAdministrativo(Guid id, AtualizarContaAdministrativa dados)
    {
        var email = dados.Email.Trim().ToLowerInvariant();
        lock (gate)
        {
            var conta = contas.Find(c => c.Id == id);
            if (conta == null || contas.Any(c => c.Id != id && c.Email == email)) return null;
            var hash = string.IsNullOrWhiteSpace(dados.Senha) ? conta.SenhaHash : hasher.HashPassword(email, dados.Senha);
            var nova = conta with { Nome = dados.Nome.Trim(), Email = email, SenhaHash = hash, Cidade = dados.Cidade.Trim(), Perfil = dados.Perfil };
            Gravar(contas.Select(c => c.Id == id ? nova : c).ToList()); return nova;
        }
    }
    public bool ExcluirAdministrativo(Guid id)
    {
        lock (gate)
        {
            if (!contas.Any(c => c.Id == id)) return false;
            Gravar(contas.Where(c => c.Id != id).ToList()); return true;
        }
    }
    public Conta? Atualizar(Guid id, PerfilConta dados)
    {
        lock (gate)
        {
            var conta = contas.Find(c => c.Id == id);
            if (conta == null) return null;
            var nova = conta with { Nome = dados.Nome.Trim(), Cidade = dados.Cidade.Trim() };
            Gravar(contas.Select(c => c.Id == id ? nova : c).ToList()); return nova;
        }
    }
    public VagaSalva? Salvar(Guid id, SalvarVaga dados)
    {
        lock (gate)
        {
            var conta = contas.Find(c => c.Id == id);
            if (conta == null) return null;
            var existente = conta.Vagas.Find(v => v.Dados.IdExterno == dados.IdExterno);
            if (existente != null) return existente;
            if (conta.Vagas.Count >= 500) throw new InvalidOperationException("Limite de 500 vagas salvas. Remova uma vaga para continuar.");
            var vaga = new VagaSalva(Guid.NewGuid(), dados, DateTimeOffset.UtcNow);
            Gravar(contas.Select(c => c.Id == id ? c with { Vagas = [.. c.Vagas, vaga] } : c).ToList());
            return vaga;
        }
    }
    public bool Remover(Guid id, Guid vagaId)
    {
        lock (gate)
        {
            var conta = contas.Find(c => c.Id == id);
            if (conta == null || !conta.Vagas.Any(v => v.Id == vagaId)) return false;
            Gravar(contas.Select(c => c.Id == id ? c with { Vagas = c.Vagas.Where(v => v.Id != vagaId).ToList() } : c).ToList());
            return true;
        }
    }
    private void Gravar(List<Conta> novas)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
        File.WriteAllText(arquivo + ".tmp", JsonSerializer.Serialize(novas, json));
        File.Move(arquivo + ".tmp", arquivo, true); contas = novas;
    }
}
