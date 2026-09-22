$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$projectRoot = Split-Path $PSScriptRoot -Parent
$apiRoot = Join-Path $projectRoot 'Projeto010926.API'
$dll = Join-Path $apiRoot 'bin/Debug/net10.0/Projeto010926.API.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'Execute dotnet build antes do teste.' }
$testRoot = Join-Path $apiRoot ('App_Data/smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$baseUrl = "http://127.0.0.1:$port"
$client = New-Object System.Net.Http.HttpClient
$client.Timeout = [TimeSpan]::FromSeconds(5)
$key = [Guid]::NewGuid().ToString('N')
$saved = @{}
foreach ($name in @('Vagas__Arquivo','Vagas__ChaveAdministracao','ASPNETCORE_ENVIRONMENT','JSearch__ApiKey')) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$env:Vagas__Arquivo = Join-Path $testRoot 'vagas.json'
$env:Vagas__ChaveAdministracao = $key
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:JSearch__ApiKey = ' '
$script:server = $null
$script:checks = 0

function Request($method, $path, $body = $null, $authenticated = $false) {
    $message = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::new($method), "$baseUrl$path")
    try {
        if ($authenticated) { $message.Headers.Add('X-Api-Key', $key) }
        if ($null -ne $body) {
            $message.Content = [System.Net.Http.StringContent]::new(($body | ConvertTo-Json -Depth 10), [Text.Encoding]::UTF8, 'application/json')
        }
        $response = $client.SendAsync($message).GetAwaiter().GetResult()
        try {
            $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $json = if ($text) { $text | ConvertFrom-Json } else { $null }
            return @{ Status = [int]$response.StatusCode; Json = $json }
        } finally { $response.Dispose() }
    } finally { $message.Dispose() }
}
function Check($condition, $label) {
    if (!$condition) { throw "FALHOU: $label" }
    $script:checks++
    Write-Output "OK: $label"
}
function Start-Api {
    $script:server = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @('"' + $dll + '"', '--urls', $baseUrl) -WorkingDirectory $apiRoot -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $testRoot 'stdout.log') -RedirectStandardError (Join-Path $testRoot 'stderr.log')
    for ($i = 0; $i -lt 60; $i++) {
        if ($script:server.HasExited) { throw "Servidor encerrou. Veja $testRoot" }
        try { if ((Request 'GET' '/api').Status -eq 200) { return } } catch { }
        Start-Sleep -Milliseconds 250
    }
    throw 'Servidor nao iniciou.'
}
function Stop-Api {
    if ($null -ne $script:server -and !$script:server.HasExited) {
        Stop-Process -Id $script:server.Id
        $script:server.WaitForExit()
    }
}
try {
    Start-Api
    Check ((Request 'GET' '/api/vagas').Json.total -eq 0) 'Base inicialmente vazia'
    Check ((Request 'GET' '/api/vagas/externas').Status -eq 503) 'Busca externa sem chave retorna 503'
    Check ((Request 'GET' '/api/vagas/externas?pagina=0').Status -eq 400) 'Pagina externa invalida'
    Check ((Request 'GET' '/api/vagas/externas?estado=ZZ').Status -eq 400) 'Estado externo invalido'
    $vaga = @{ titulo = 'Desenvolvedor .NET'; empresa = 'Teste'; descricao = 'Somente teste'; linkCandidatura = 'https://example.com/teste'; salario = 3500; latitude = -7.12; longitude = -34.86 }
    Check ((Request 'POST' '/api/vagas' $vaga).Status -eq 401) 'Escrita sem chave bloqueada'
    Check ((Request 'POST' '/api/vagas' @{} $true).Status -eq 400) 'Campos obrigatorios'
    $invalida = $vaga.Clone(); $invalida.linkCandidatura = 'javascript:alert(1)'
    Check ((Request 'POST' '/api/vagas' $invalida $true).Status -eq 400) 'Link invalido rejeitado'
    $invalida = $vaga.Clone(); $invalida.expiraEm = '2000-01-01T00:00:00Z'
    Check ((Request 'POST' '/api/vagas' $invalida $true).Status -eq 400) 'Expiracao passada rejeitada'
    $criada = Request 'POST' '/api/vagas' $vaga $true
    Check ($criada.Status -eq 201) 'Cadastro retorna 201'
    $id = $criada.Json.id
    Check ($criada.Json.dados.cidade -eq ('Jo' + [char]0x00e3 + 'o Pessoa') -and $criada.Json.dados.estado -eq 'PB') 'Cidade padrao'
    Check ((Request 'GET' "/api/vagas/$id").Status -eq 200) 'Detalhe da vaga'
    $outra = $vaga.Clone(); $outra.cidade = 'Recife'; $outra.estado = 'PE'
    Check ((Request 'POST' '/api/vagas' $outra $true).Status -eq 201) 'Cadastro em outra cidade'
    Check ((Request 'GET' '/api/vagas').Json.total -eq 1) 'Busca padrao exclui outra cidade'
    Check ((Request 'GET' '/api/vagas?cidade=Recife&estado=PE').Json.total -eq 1) 'Troca de cidade'
    Check ((Request 'GET' '/api/vagas?cidade=joao%20pessoa&termo=DESENVOLVEDOR').Json.total -eq 1) 'Busca ignora acentos e maiusculas'
    Check ((Request 'GET' '/api/vagas?salarioMinimo=4000').Json.total -eq 0) 'Filtro salarial'
    Check ((Request 'GET' '/api/vagas?modalidade=remoto').Json.total -eq 0) 'Filtro de modalidade'
    Check ((Request 'GET' '/api/vagas?contrato=pj').Json.total -eq 0) 'Filtro de contrato'
    Check ((Request 'GET' '/api/vagas?pagina=0').Status -eq 400) 'Pagina invalida'
    Check ((Request 'GET' '/api/vagas?tamanhoPagina=101').Status -eq 400) 'Limite de pagina'
    Check ((Request 'GET' '/api/vagas?pagina=2&tamanhoPagina=1').Json.itens.Count -eq 0) 'Paginacao'
    Check ((Request 'GET' '/api/vagas?latitude=-7.12').Status -eq 400) 'Coordenadas incompletas rejeitadas'
    Check ((Request 'GET' '/api/vagas?latitude=-7.12&longitude=-34.86&raioKm=1').Json.total -eq 1) 'Vaga dentro do raio'
    Check ((Request 'GET' '/api/vagas?latitude=0&longitude=0&raioKm=1').Json.total -eq 0) 'Vaga fora do raio'
    Check ((Request 'GET' '/openapi/v1.json').Status -eq 200) 'OpenAPI'
    Stop-Api
    Start-Api
    Check ((Request 'GET' '/api/vagas').Json.total -eq 1) 'Persistencia apos reinicio'
    Check ((Request 'PATCH' "/api/vagas/$id/encerrar").Status -eq 401) 'Encerramento protegido'
    Check ((Request 'PATCH' "/api/vagas/$id/encerrar" $null $true).Status -eq 204) 'Encerramento'
    Check ((Request 'GET' '/api/vagas').Json.total -eq 0) 'Encerradas fora da busca'
    Check ((Request 'GET' "/api/vagas/$id").Json.ativa -eq $false) 'Detalhe informa encerramento'
    Check ((Request 'GET' '/api/vagas/00000000-0000-0000-0000-000000000000').Status -eq 404) 'Vaga inexistente'
    Stop-Api
    $env:Vagas__ChaveAdministracao = ''
    Start-Api
    Check ((Request 'POST' '/api/vagas' $vaga $true).Status -eq 503) 'Sem chave configurada, escrita desabilitada'
    Write-Output "$script:checks verificacoes passaram. Logs e base de teste: $testRoot"
} finally {
    Stop-Api
    $client.Dispose()
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
