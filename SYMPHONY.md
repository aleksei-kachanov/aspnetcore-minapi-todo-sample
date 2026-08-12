# Project Symphony — E2E Test Target

This repo is a dedicated test target for [Project Symphony](https://github.com/aleksei-kachanov/project-symphony) live E2E runs.

## Archetype

`dotnet-rest-api-service` — .NET 9 ASP.NET Core REST API.

> **Note**: Symphony's auto-detection (`detect_repo_archetype`) does not yet detect `.sln` files.
> Set `SYMPHONY_ARCHETYPE=dotnet-rest-api-service` explicitly until the detection rule is added.

## Setup

```bash
git clone git@github.com:aleksei-kachanov/aspnetcore-minapi-todo-sample.git
cd aspnetcore-minapi-todo-sample

# Verify it builds and tests pass
dotnet test

# Configure Symphony
export SYMPHONY_SOURCE_ROOT=$(pwd)
export SYMPHONY_ARCHETYPE=dotnet-rest-api-service
export GITHUB_REPOSITORY=aleksei-kachanov/aspnetcore-minapi-todo-sample
export SYMPHONY_DELIVERY_MODE=live
export SYMPHONY_AUTO_MERGE=false
export MISTRAL_API_KEY=<your-key>

# Run a test epic
uv run project_symphony "As a developer I want a GET /health endpoint \
  that returns HTTP 200 with {status: ok} so I can verify the service is running"
```

## Good First Epics (SIMPLE tier)

| Epic | Expected output |
|---|---|
| Add `GET /health` returning `{status: "ok", version: "1.0"}` | `HealthEndpoints.cs` + register in `Program.cs` |
| Add `due_date` field to `TodoItem` | `Todo.cs` + new EF migration + service update |
| Add pagination (`?page=1&size=10`) to `GET /todos/v1` | Service + endpoint query param |
| Add `GET /todos/v1/{id}` returns 404 when not found | Endpoint handler + unit test |

## Build and Test Commands

Symphony's `dotnet-rest-api-service` archetype uses:

```bash
dotnet build   # compile gate
dotnet test    # test gate (outputs TRX to TestResults/)
```
