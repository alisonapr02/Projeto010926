using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Projeto010926.API.Models;
using Projeto010926.API.Services;

namespace Projeto010926.API.Controllers;

[ApiController]
[Route("api/vagas")]
public sealed class VagasController(VagaStore store, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public IActionResult Buscar([FromQuery] BuscaVagas busca)
    {
        var agora = DateTimeOffset.UtcNow;
        var itens = store.Listar().Where(v => v.Ativa && (v.Dados.ExpiraEm == null || v.Dados.ExpiraEm > agora))
            .Where(v => string.IsNullOrWhiteSpace(busca.Termo) || Contem(v.Dados.Titulo, busca.Termo) ||
                Contem(v.Dados.Descricao, busca.Termo) || Contem(v.Dados.Empresa, busca.Termo))
            .Where(v => Igual(v.Dados.Cidade, busca.Cidade) && Igual(v.Dados.Estado, busca.Estado))
            .Where(v => busca.Modalidade == null || v.Dados.Modalidade == busca.Modalidade)
            .Where(v => busca.Contrato == null || v.Dados.Contrato == busca.Contrato)
            .Where(v => busca.SalarioMinimo == null || v.Dados.Salario >= busca.SalarioMinimo)
            .Select(v => new { Vaga = v, DistanciaKm = Distancia(v.Dados, busca) })
            .Where(v => busca.RaioKm == null || v.DistanciaKm <= busca.RaioKm)
            .OrderBy(v => busca.RaioKm.HasValue ? v.DistanciaKm : 0)
            .ThenByDescending(v => v.Vaga.PublicadaEm).ThenBy(v => v.Vaga.Id).ToArray();
        return Ok(new
        {
            total = itens.Length, busca.Pagina, busca.TamanhoPagina,
            totalPaginas = (int)Math.Ceiling((double)itens.Length / busca.TamanhoPagina),
            itens = itens.Skip((busca.Pagina - 1) * busca.TamanhoPagina).Take(busca.TamanhoPagina)
        });
    }

    [HttpGet("{id:guid}")]
    public IActionResult Obter(Guid id)
    {
        var vaga = store.Listar().FirstOrDefault(v => v.Id == id);
        return vaga == null ? NotFound() : Ok(new
        {
            vaga.Id, vaga.Dados, vaga.PublicadaEm,
            ativa = vaga.Ativa && (vaga.Dados.ExpiraEm == null || vaga.Dados.ExpiraEm > DateTimeOffset.UtcNow)
        });
    }

    [HttpPost]
    public IActionResult Cadastrar(CadastroVaga dados)
    {
        var erro = Autorizar();
        if (erro != null) return erro;
        var vaga = store.Criar(dados);
        return CreatedAtAction(nameof(Obter), new { id = vaga.Id }, vaga);
    }

    [HttpPatch("{id:guid}/encerrar")]
    public IActionResult Encerrar(Guid id)
    {
        var erro = Autorizar();
        if (erro != null) return erro;
        return store.Encerrar(id) ? NoContent() : NotFound();
    }

    private IActionResult? Autorizar()
    {
        var chave = configuration["Vagas:ChaveAdministracao"];
        if (string.IsNullOrWhiteSpace(chave))
            return Problem(statusCode: 503, detail: "Configure Vagas__ChaveAdministracao para cadastrar ou encerrar vagas.");
        var recebida = Request.Headers["X-Api-Key"].ToString();
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(chave)),
            SHA256.HashData(Encoding.UTF8.GetBytes(recebida))) ? null : Unauthorized();
    }

    private static bool Igual(string a, string b) => CultureInfo.InvariantCulture.CompareInfo.Compare(
        a, b.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;
    private static bool Contem(string a, string b) => CultureInfo.InvariantCulture.CompareInfo.IndexOf(
        a, b.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    private static double? Distancia(CadastroVaga vaga, BuscaVagas busca)
    {
        if (busca.Latitude == null || busca.Longitude == null || vaga.Latitude == null || vaga.Longitude == null)
            return null;
        const double rad = Math.PI / 180;
        var lat = (vaga.Latitude.Value - busca.Latitude.Value) * rad;
        var lon = (vaga.Longitude.Value - busca.Longitude.Value) * rad;
        var a = Math.Pow(Math.Sin(lat / 2), 2) + Math.Cos(busca.Latitude.Value * rad) *
            Math.Cos(vaga.Latitude.Value * rad) * Math.Pow(Math.Sin(lon / 2), 2);
        return 6371 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
}
