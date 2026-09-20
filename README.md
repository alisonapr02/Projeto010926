# Projeto010926

API ASP.NET Core em .NET 10, com endpoint de exemplo de previsao do tempo.

## Executar

Com o SDK do .NET 10 instalado, execute na raiz do projeto:

```sh
dotnet run --project Projeto010926.API --launch-profile http
```

- Endpoint: `http://localhost:5003/weatherforecast`
- OpenAPI em desenvolvimento: `http://localhost:5003/openapi/v1.json`

O arquivo `Projeto010926.API/Projeto010926.API.http` contem uma requisicao de exemplo.
