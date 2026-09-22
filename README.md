# API de empregos — João Pessoa/PB

API ASP.NET Core (.NET 10) para cadastrar e localizar vagas. A busca começa em João Pessoa/PB; cidade e estado podem ser alterados por parâmetros.

**A base local começa vazia.** As vagas locais são cadastradas por um administrador. A rota `/api/vagas/externas` consulta a JSearch; exige chave e plano habilitado no fornecedor. Não há integração com SINE ou mapa visual nesta versão.

## Executar

Instale o SDK .NET 10 e execute na raiz:

```powershell
$env:Vagas__ChaveAdministracao = 'substitua-por-uma-chave-longa-e-privada'
dotnet run --project Projeto010926.API --launch-profile http
```

Abra `http://localhost:5003/api/vagas`. A busca é pública. Para cadastrar ou encerrar vagas, envie a mesma chave no cabeçalho `X-Api-Key`. Sem chave configurada, essas operações retornam 503; chave incorreta retorna 401. Não coloque sua chave no GitHub. O perfil HTTP é para uso local; em implantação, configure HTTPS.

## Rotas

| Método | Rota | Função |
| --- | --- | --- |
| GET | `/api/vagas` | Buscar vagas ativas e não expiradas |
| GET | `/api/vagas/{id}` | Consultar uma vaga, inclusive encerrada |
| POST | `/api/vagas` | Cadastrar vaga (chave obrigatória) |
| PATCH | `/api/vagas/{id}/encerrar` | Encerrar vaga (chave obrigatória) |
| GET | `/openapi/v1.json` | Especificação OpenAPI em Development |

## Filtros

- `cidade` e `estado`: padrões `João Pessoa` e `PB`. A comparação de cidade ignora acentos e maiúsculas.
- `termo`: pesquisa em título, descrição e empresa, ignorando acentos e maiúsculas.
- `modalidade`: `presencial`, `hibrido`, `remoto`.
- `contrato`: `clt`, `pj`, `estagio`, `temporario`, `aprendiz`.
- `salarioMinimo`: valor mensal em reais; vagas sem salário informado ficam fora desse filtro.
- `pagina` e `tamanhoPagina`: padrões 1 e 20; máximo de 100 itens por página.
- `latitude`, `longitude` e `raioKm`: devem ser enviados juntos. Raio de 0,1 a 500 km, em linha reta. Vagas sem coordenadas ficam fora; os filtros de cidade e estado continuam valendo. A ordenação é por proximidade, depois por publicação mais recente. Sem raio, a ordem é por publicação mais recente.

Exemplos:

```text
http://localhost:5003/api/vagas?termo=desenvolvedor&modalidade=remoto
http://localhost:5003/api/vagas?cidade=Joao%20Pessoa&estado=PB&contrato=clt
http://localhost:5003/api/vagas?latitude=-7.12&longitude=-34.86&raioKm=10
```

A resposta contém `total`, `pagina`, `tamanhoPagina`, `totalPaginas` e `itens`. Cada item inclui `vaga` e `distanciaKm` (nulo quando não calculada).

## Cadastro

Veja as requisições prontas em `Projeto010926.API/Projeto010926.API.http`. O exemplo usa empresa e link fictícios apenas para teste. Substitua-os por informações verificadas antes de cadastrar oportunidades reais.

Campos obrigatórios: `titulo`, `empresa`, `descricao` e `linkCandidatura` (HTTP/HTTPS). Cidade, estado, modalidade e contrato têm padrões João Pessoa, PB, presencial e clt. `salario`, coordenadas e `expiraEm` são opcionais; a expiração deve incluir fuso horário e ser futura. Dados inválidos retornam 400 com detalhes.

## Armazenamento

Os dados persistem em `Projeto010926.API/App_Data/vagas.json`, excluído do Git. É possível mudar o caminho com `Vagas__Arquivo`. A gravação usa arquivo temporário e substituição; erros não descartam silenciosamente a base existente. Faça backup desse arquivo para preservar as vagas.

Este armazenamento atende uma única instância local. Para múltiplas instâncias, será necessário migrar para um banco de dados compartilhado.

## Verificar

```powershell
dotnet build Projeto010926.slnx
powershell -ExecutionPolicy Bypass -File scripts/smoke-test.ps1
```

O teste usa porta e base temporária próprias, sem alterar as vagas da aplicação.

Referência: [documentação oficial do ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0).

## Busca de vagas reais — JSearch

Acesse `http://localhost:5003/api/vagas/externas` para consultar João Pessoa/PB. Filtros disponíveis: `termo`, `cidade`, `estado` e `cursor`. Exemplo:

```text
http://localhost:5003/api/vagas/externas?termo=atendente
```

Configure `JSearch__ApiKey` no ambiente ou use `Projeto010926.API/appsettings.Local.json` com esta estrutura (somente placeholder):

```json
{ "JSearch": { "ApiKey": "SUA_CHAVE" } }
```

Esse arquivo local é ignorado pelo Git e não é copiado para a publicação. Variáveis de ambiente têm prioridade; reinicie a API após alterar a configuração. A chave fica no servidor, não na URL nem na resposta.

É necessário habilitar o plano da **JSearch** na conta OpenWeb Ninja: criar uma chave, por si só, pode não liberar acesso. Um erro 502 com a mensagem de acesso recusado indica que o fornecedor retornou 403. Verifique chave, assinatura e permissões no painel; a aplicação não ativa planos nem contrata serviços automaticamente.

As respostas incluem `fonte`, `consulta`, `proximoCursor`, `quantidade`, `consultadoEm` e `itens`. `quantidade` é o número recebido nesta página, não o total de vagas disponíveis. A localização retornada pelo fornecedor é preservada e pode incluir vagas remotas ou cidades próximas. Esta rota não aceita os filtros locais de salário, contrato ou raio.

Cada consulta usa uma página do fornecedor. Resultados ficam em memória por 30 minutos para poupar a cota; o cache é perdido ao reiniciar. A API não faz novas tentativas automáticas em caso de falha. As vagas externas não são gravadas no cadastro local. Um resultado vazio é diferente de falha: indisponibilidade, chave ausente, limite de consultas e timeout retornam erros explícitos.

Documentação do fornecedor: https://www.openwebninja.com/api/jsearch

A busca externa usa a rota search-v2. Para continuar, envie o proximoCursor retornado no parametro cursor, mantendo os mesmos filtros. Cursor ausente inicia a busca; cursor nulo na resposta indica que o fornecedor nao forneceu continuacao. O parametro pagina maior que 1 e rejeitado nessa rota. A paginacao local em /api/vagas permanece numerica.

## Página de busca

Abra **http://localhost:5003/** para usar a interface visual Perto, feita em HTML, CSS e JavaScript e servida pela própria API ASP.NET Core. Não é necessário instalar PHP ou Node.js.

A página busca vagas da JSearch ao abrir. Permite pesquisar cargo, cidade e estado, ver detalhes e abrir o site de candidatura em outra aba. O filtro Remotas considera apenas as oportunidades já carregadas; Carregar mais usa o cursor do fornecedor e pode consumir outra consulta. Resultados repetidos são removidos da lista.

O resumo técnico da API está em `/api`. O código da interface fica em `Projeto010926.API/wwwroot/`. A chave permanece no servidor.

## Configuração administrativa local

Para acessar /admin, configure Administracao__Usuario e Administracao__Senha no ambiente ou a seção Administracao (Usuario e Senha) em Projeto010926.API/appsettings.Local.json. A senha não é incluída no repositório. O arquivo local, as chaves da API e os dados de App_Data ficam fora do Git e precisam de backup separado.
