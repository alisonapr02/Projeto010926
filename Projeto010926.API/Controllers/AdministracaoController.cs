using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Projeto010926.API.Models;
using Projeto010926.API.Services;

namespace Projeto010926.API.Controllers;

[ApiController, Route("api/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdministracaoController(ContaStore store, IConfiguration configuration, IAntiforgery antiforgery) : ControllerBase
{
    public const string Scheme = "AdminCookie";
    private const string Policy = "AdminOnly";

    [HttpGet("csrf")]
    public IActionResult Csrf() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("login"), EnableRateLimiting("contas")]
    public async Task<IActionResult> Login(LoginAdministracao dados)
    {
        var usuario = configuration["Administracao:Usuario"] ?? "admin";
        var senha = configuration["Administracao:Senha"];
        if (string.IsNullOrWhiteSpace(senha)) return Problem(statusCode: 503, detail: "Senha administrativa nao configurada.");
        var usuarioValido = CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(usuario)), SHA256.HashData(Encoding.UTF8.GetBytes(dados.Usuario.Trim())));
        var senhaValida = CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(senha)), SHA256.HashData(Encoding.UTF8.GetBytes(dados.Senha)));
        if (!usuarioValido || !senhaValida) return Problem(statusCode: 401, detail: "Usuário ou senha administrativa incorretos.");
        await HttpContext.SignInAsync(Scheme, new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("admin", "true"), new Claim(ClaimTypes.Name, usuario)
        ], Scheme)), new AuthenticationProperties { IsPersistent = false });
        return Ok(new { usuario });
    }

    [Authorize(AuthenticationSchemes = Scheme, Policy = Policy), HttpPost("sair")]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(Scheme); return NoContent();
    }

    [Authorize(AuthenticationSchemes = Scheme, Policy = Policy), HttpGet("usuarios")]
    public IActionResult Listar() => Ok(store.Listar().OrderByDescending(c => c.CriadaEm).Select(Resumo));

    [Authorize(AuthenticationSchemes = Scheme, Policy = Policy), HttpGet("usuarios/{id:guid}")]
    public IActionResult Obter(Guid id) => store.Obter(id) is { } conta ? Ok(Detalhe(conta)) : NotFound();

    [Authorize(AuthenticationSchemes = Scheme, Policy = Policy), IgnoreAntiforgeryToken, HttpPost("usuarios")]
    public IActionResult Criar(CriarContaAdministrativa dados) => store.CriarAdministrativo(dados) is { } conta
        ? Created($"/api/admin/usuarios/{conta.Id}", Detalhe(conta))
        : Problem(statusCode: 409, detail: "Já existe uma conta com esse e-mail.");

    [Authorize(AuthenticationSchemes = Scheme, Policy = Policy), IgnoreAntiforgeryToken, HttpPut("usuarios/{id:guid}")]
    public IActionResult Atualizar(Guid id, AtualizarContaAdministrativa dados)
    {
        var conta = store.AtualizarAdministrativo(id, dados);
        if (conta == null) return Problem(statusCode: 409, detail: "Conta inexistente ou e-mail já utilizado.");
        return Ok(Detalhe(conta));
    }

    [Authorize(AuthenticationSchemes = Scheme, Policy = Policy), IgnoreAntiforgeryToken, HttpDelete("usuarios/{id:guid}")]
    public IActionResult Excluir(Guid id, ExcluirContaAdministrativa dados)
    {
        var conta = store.Obter(id);
        if (conta == null) return NotFound();
        if (conta.Perfil == "administrador")
        {
            var senhaPrincipal = configuration["Administracao:Senha"];
            if (string.IsNullOrWhiteSpace(senhaPrincipal) || !CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(Encoding.UTF8.GetBytes(senhaPrincipal)),
                    SHA256.HashData(Encoding.UTF8.GetBytes(dados.SenhaPrincipal ?? ""))))
                return Problem(statusCode: 403, detail: "A senha principal é necessária para excluir outro administrador.");
        }
        return store.ExcluirAdministrativo(id) ? NoContent() : NotFound();
    }

    private static ContaAdministrativaResumo Resumo(Conta conta) =>
        new(conta.Id, conta.Nome, conta.Email, conta.Cidade, conta.CriadaEm, conta.Vagas.Count, conta.Perfil);

    private static ContaAdministrativaDetalhe Detalhe(Conta conta) =>
        new(conta.Id, conta.Nome, conta.Email, conta.Cidade, conta.CriadaEm, conta.Vagas, conta.Perfil);
}
