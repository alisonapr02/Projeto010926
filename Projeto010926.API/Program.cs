using Projeto010926.API.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<VagaStore>();
builder.Services.AddSingleton<ContaStore>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "VagaPerto.Sessao";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthentication().AddCookie("AdminCookie", options =>
{
    options.Cookie.Name = "VagaPerto.Admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy =>
    policy.AddAuthenticationSchemes("AdminCookie").RequireAuthenticatedUser().RequireClaim("admin", "true")));
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-Token");
var keysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection().SetApplicationName("VagaPerto").PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("contas", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
});
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("JSearch", client =>
{
    client.BaseAddress = new Uri("https://api.openwebninja.com/jsearch/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<JSearchService>();
var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
else app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
foreach (var pagina in new[] { "/login", "/cadastro", "/minha-conta", "/minhas-vagas" })
    app.MapGet(pagina, () => Results.File(Path.Combine(app.Environment.WebRootPath, "conta.html"), "text/html; charset=utf-8"));
app.MapGet("/admin", () => Results.File(Path.Combine(app.Environment.WebRootPath, "admin.html"), "text/html; charset=utf-8"));
app.MapGet("/api", () => Results.Ok(new
{
    nome = "API de vagas de emprego", cidadePadrao = "Brasil", estadoPadrao = "",
    vagas = "/api/vagas", documentacao = "/openapi/v1.json (Development)",
    vagasExternas = "/api/vagas/externas",
    origem = "Vagas locais em /api/vagas e busca JSearch em /api/vagas/externas."
}));
app.MapControllers();
app.Run();
