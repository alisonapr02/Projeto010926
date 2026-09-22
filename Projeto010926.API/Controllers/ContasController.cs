using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Projeto010926.API.Models;
using Projeto010926.API.Services;

namespace Projeto010926.API.Controllers;

[ApiController, Route("api/conta"), AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ContasController(ContaStore store, IAntiforgery antiforgery) : ControllerBase
{
    private Guid UsuarioId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static object Perfil(Conta c) => new { c.Id, c.Nome, c.Email, c.Cidade, c.CriadaEm, vagasSalvas = c.Vagas.Count };

    [HttpGet("csrf")]
    public IActionResult Csrf() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("cadastro"), EnableRateLimiting("contas")]
    public async Task<IActionResult> Cadastrar(CadastroConta dados)
    {
        var conta = store.Criar(dados);
        if (conta == null) return Problem(statusCode: 409, detail: "Já existe uma conta com esse e-mail. Entre com sua senha.");
        await Entrar(conta); return StatusCode(201, Perfil(conta));
    }
    [HttpPost("login"), EnableRateLimiting("contas")]
    public async Task<IActionResult> Login(LoginConta dados)
    {
        var conta = store.Autenticar(dados);
        if (conta == null) return Problem(statusCode: 401, detail: "E-mail ou senha incorretos.");
        await Entrar(conta); return Ok(Perfil(conta));
    }
    [Authorize, HttpPost("sair")]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return NoContent();
    }
    [Authorize, HttpGet]
    public IActionResult Obter() => store.Obter(UsuarioId) is { } c ? Ok(Perfil(c)) : Unauthorized();
    [Authorize, HttpPut]
    public IActionResult Atualizar(PerfilConta dados) => store.Atualizar(UsuarioId, dados) is { } c ? Ok(Perfil(c)) : Unauthorized();
    [Authorize, HttpGet("vagas")]
    public IActionResult Vagas() => store.Obter(UsuarioId) is { } c ? Ok(c.Vagas.OrderByDescending(v => v.SalvaEm)) : Unauthorized();
    [Authorize, HttpPost("vagas")]
    public IActionResult Salvar(SalvarVaga dados)
    {
        try { return store.Salvar(UsuarioId, dados) is { } v ? Ok(v) : Unauthorized(); }
        catch (InvalidOperationException ex) { return Problem(statusCode: 409, detail: ex.Message); }
    }
    [Authorize, HttpDelete("vagas/{id:guid}")]
    public IActionResult Remover(Guid id) => store.Remover(UsuarioId, id) ? NoContent() : NotFound();

    private Task Entrar(Conta conta) => HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, conta.Id.ToString())],
            CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IsPersistent = false });
}
