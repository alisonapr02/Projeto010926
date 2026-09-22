using System.ComponentModel.DataAnnotations;

namespace Projeto010926.API.Models;

/// <summary>Credenciais usadas para entrar no painel administrativo.</summary>
public sealed record LoginAdministracao
{
    [Required, StringLength(100)] public string Usuario { get; init; } = "";
    [Required, StringLength(128)] public string Senha { get; init; } = "";
}

/// <summary>Dados necessários para criar uma conta pelo painel administrativo.</summary>
public sealed record CriarContaAdministrativa : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 2)] public string Nome { get; init; } = "";
    [Required, StringLength(254)] public string Email { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 10)] public string Senha { get; init; } = "";
    [Required, StringLength(100)] public string Cidade { get; init; } = "João Pessoa";
    [Required, RegularExpression("^(usuario|administrador|programador)$")] public string Perfil { get; init; } = "usuario";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Perfil == "usuario" && !new EmailAddressAttribute().IsValid(Email))
            yield return new("Usuário comum precisa de um e-mail válido.", [nameof(Email)]);
    }
}

/// <summary>Dados editáveis de uma conta pelo painel administrativo.</summary>
public sealed record AtualizarContaAdministrativa : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 2)] public string Nome { get; init; } = "";
    [Required, StringLength(254)] public string Email { get; init; } = "";
    [Required, StringLength(100)] public string Cidade { get; init; } = "João Pessoa";
    [Required, RegularExpression("^(usuario|administrador|programador)$")] public string Perfil { get; init; } = "usuario";
    [StringLength(128, MinimumLength = 10)] public string? Senha { get; init; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Perfil == "usuario" && !new EmailAddressAttribute().IsValid(Email))
            yield return new("Usuário comum precisa de um e-mail válido.", [nameof(Email)]);
    }
}

/// <summary>Confirmação necessária para excluir uma conta administrativa.</summary>
public sealed record ExcluirContaAdministrativa
{
    [StringLength(128)] public string? SenhaPrincipal { get; init; }
}

/// <summary>Resumo de uma conta exibido na lista administrativa.</summary>
public sealed record ContaAdministrativaResumo(Guid Id, string Nome, string Email, string Cidade,
    DateTimeOffset CriadaEm, int VagasSalvas, string Perfil);

/// <summary>Dados completos de uma conta para consulta administrativa.</summary>
public sealed record ContaAdministrativaDetalhe(Guid Id, string Nome, string Email, string Cidade,
    DateTimeOffset CriadaEm, IReadOnlyList<VagaSalva> Vagas, string Perfil);
