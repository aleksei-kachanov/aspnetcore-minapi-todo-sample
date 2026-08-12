# ASP.NET Core Minimal API — Todo Sample

ASP.NET Core 9 Minimal API with versioned route groups, EF Core SQLite, JWT auth, Swagger, and a full test suite (integration + unit tests).

Forked from [dotnet/AspNetCore.Docs.Samples](https://github.com/dotnet/AspNetCore.Docs.Samples/tree/main/fundamentals/minimal-apis/samples/MinApiTestsSample) and upgraded to .NET 9.

## Features

- **Versioned route groups**: `/todos/v1` and `/todos/v2`
- **EF Core + SQLite** persistence with migrations
- **Service abstractions**: `ITodoService`, `IEmailService`
- **JWT Bearer** authentication
- **Swagger / OpenAPI** at `/swagger`
- **Integration tests** using `WebApplicationFactory<Program>`
- **Unit tests** using xUnit + Moq + EF Core InMemory

## Project Structure

```
aspnetcore-minapi-todo-sample/
├── MinApiTestsSample.sln
├── WebMinRouteGroup/          # ASP.NET Core 9 app
│   ├── Program.cs
│   ├── TodoEndpointsV1.cs     # /todos/v1 route group
│   ├── TodoEndpointsV2.cs     # /todos/v2 route group (email on create)
│   ├── Data/                  # Todo entity, DTO, DbContext
│   ├── Services/              # ITodoService, IEmailService
│   └── Migrations/
├── IntegrationTests/          # WebApplicationFactory-based tests
│   └── Helpers/               # TestWebApplicationFactory, TestAuthHandler
└── UnitTests/                 # xUnit + Moq tests
    └── Helpers/               # MockDb helper
```

## Running

```bash
dotnet run --project WebMinRouteGroup
# App at http://localhost:5000
# Swagger at http://localhost:5000/swagger
```

## Testing

```bash
dotnet test
```

## Project Symphony — E2E Test Target

This repo is configured as a Symphony test target. See [SYMPHONY.md](SYMPHONY.md).

```bash
export SYMPHONY_SOURCE_ROOT=$(pwd)
export SYMPHONY_ARCHETYPE=dotnet-rest-api-service
export GITHUB_REPOSITORY=aleksei-kachanov/aspnetcore-minapi-todo-sample
```

## License

Sample code from [Microsoft Docs](https://github.com/dotnet/AspNetCore.Docs.Samples), MIT License.
