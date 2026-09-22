using System.ComponentModel.DataAnnotations;

namespace Projeto010926.API.Models;

public sealed record CadastroVaga : IValidatableObject
{
    [Required, StringLength(150)] public string Titulo { get; init; } = "";
    [Required, StringLength(150)] public string Empresa { get; init; } = "";
    [Required, StringLength(5000)] public string Descricao { get; init; } = "";
    [Required, StringLength(100)] public string Cidade { get; init; } = "João Pessoa";
    [Required, RegularExpression("(?i)^(AC|AL|AP|AM|BA|CE|DF|ES|GO|MA|MT|MS|MG|PA|PB|PR|PE|PI|RJ|RN|RS|RO|RR|SC|SP|SE|TO)$")]
    public string Estado { get; init; } = "PB";
    [Required, RegularExpression("^(presencial|hibrido|remoto)$")]
    public string Modalidade { get; init; } = "presencial";
    [Required, RegularExpression("^(clt|pj|estagio|temporario|aprendiz)$")]
    public string Contrato { get; init; } = "clt";
    [Range(0, 100000000)] public decimal? Salario { get; init; }
    [Required, StringLength(2000)] public string LinkCandidatura { get; init; } = "";
    [Range(-90, 90)] public double? Latitude { get; init; }
    [Range(-180, 180)] public double? Longitude { get; init; }
    public DateTimeOffset? ExpiraEm { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!Uri.TryCreate(LinkCandidatura, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
            yield return new("Informe um link de candidatura HTTP ou HTTPS.", [nameof(LinkCandidatura)]);
        if (Latitude.HasValue != Longitude.HasValue)
            yield return new("Informe latitude e longitude juntas.", [nameof(Latitude), nameof(Longitude)]);
        if (ExpiraEm <= DateTimeOffset.UtcNow)
            yield return new("A expiração deve ser futura.", [nameof(ExpiraEm)]);
    }
}

public sealed record Vaga(Guid Id, CadastroVaga Dados, DateTimeOffset PublicadaEm, bool Ativa);

public sealed class BuscaVagas : IValidatableObject
{
    [StringLength(150)] public string? Termo { get; init; }
    [Required, StringLength(100)] public string Cidade { get; init; } = "João Pessoa";
    [Required, RegularExpression("(?i)^(AC|AL|AP|AM|BA|CE|DF|ES|GO|MA|MT|MS|MG|PA|PB|PR|PE|PI|RJ|RN|RS|RO|RR|SC|SP|SE|TO)$")]
    public string Estado { get; init; } = "PB";
    [RegularExpression("^(presencial|hibrido|remoto)$")] public string? Modalidade { get; init; }
    [RegularExpression("^(clt|pj|estagio|temporario|aprendiz)$")] public string? Contrato { get; init; }
    [Range(0, 100000000)] public decimal? SalarioMinimo { get; init; }
    [Range(-90, 90)] public double? Latitude { get; init; }
    [Range(-180, 180)] public double? Longitude { get; init; }
    [Range(0.1, 500)] public double? RaioKm { get; init; }
    [Range(1, 1000000)] public int Pagina { get; init; } = 1;
    [Range(1, 100)] public int TamanhoPagina { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        var quantidade = (Latitude.HasValue ? 1 : 0) + (Longitude.HasValue ? 1 : 0) + (RaioKm.HasValue ? 1 : 0);
        if (quantidade != 0 && quantidade != 3)
            yield return new("Para buscar por distância, informe latitude, longitude e raioKm.",
                [nameof(Latitude), nameof(Longitude), nameof(RaioKm)]);
    }
}
