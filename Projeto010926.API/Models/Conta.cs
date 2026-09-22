using System.ComponentModel.DataAnnotations;

namespace Projeto010926.API.Models;

public sealed record CadastroConta
{
    [Required, StringLength(100, MinimumLength = 2)] public string Nome { get; init; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 10)] public string Senha { get; init; } = "";
}
public sealed record LoginConta
{
    [Required, StringLength(254)] public string Email { get; init; } = "";
    [Required, StringLength(128)] public string Senha { get; init; } = "";
}
public sealed record PerfilConta
{
    [Required, StringLength(100, MinimumLength = 2)] public string Nome { get; init; } = "";
    [Required, StringLength(100)] public string Cidade { get; init; } = "João Pessoa";
}
public sealed record SalvarVaga : IValidatableObject
{
    [Required, StringLength(8000)] public string IdExterno { get; init; } = "";
    [Required, StringLength(500)] public string Titulo { get; init; } = "";
    [StringLength(300)] public string? Empresa { get; init; }
    [StringLength(300)] public string? Localizacao { get; init; }
    [StringLength(50000)] public string? Descricao { get; init; }
    [StringLength(8000)] public string? LinkCandidatura { get; init; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (LinkCandidatura != null && (!Uri.TryCreate(LinkCandidatura, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            yield return new("Informe um link HTTP ou HTTPS.", [nameof(LinkCandidatura)]);
    }
}
public sealed record VagaSalva(Guid Id, SalvarVaga Dados, DateTimeOffset SalvaEm);
public sealed record Conta(Guid Id, string Nome, string Email, string SenhaHash, string Cidade,
    DateTimeOffset CriadaEm, List<VagaSalva> Vagas, string Perfil = "usuario");
