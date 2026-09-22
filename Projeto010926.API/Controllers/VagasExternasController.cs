using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Projeto010926.API.Services;

namespace Projeto010926.API.Controllers;

[ApiController]
[Route("api/vagas/externas")]
public sealed class VagasExternasController(JSearchService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Buscar([FromQuery] BuscaExterna busca, CancellationToken ct)
    {
        try { return Ok(await service.Buscar(busca.Termo, busca.Cidade, busca.Estado, busca.Cursor, ct)); }
        catch (JSearchException ex) { return Problem(statusCode: ex.Status, detail: ex.Message); }
    }
}

public sealed class BuscaExterna
{
    [StringLength(150)] public string? Termo { get; init; }
    [StringLength(100)] public string? Cidade { get; init; }
    [RegularExpression("(?i)^(AC|AL|AP|AM|BA|CE|DF|ES|GO|MA|MT|MS|MG|PA|PB|PR|PE|PI|RJ|RN|RS|RO|RR|SC|SP|SE|TO)$")]
    public string? Estado { get; init; }
    [StringLength(8000)] public string? Cursor { get; init; }
    // Compatibilidade: páginas posteriores da busca externa exigem cursor.
    [Range(1, 1, ErrorMessage = "Use cursor para continuar a busca externa, em vez de pagina.")]
    public int Pagina { get; init; } = 1;
}
