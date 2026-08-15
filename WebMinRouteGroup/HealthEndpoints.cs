using Microsoft.EntityFrameworkCore;
using WebMinRouteGroup.Data;

namespace WebMinRouteGroup;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthApi(this RouteGroupBuilder group)
    {
        group.MapGet("/health", GetHealth)
            .AllowAnonymous();

        return group;
    }

    public static async Task<IResult> GetHealth(TodoGroupDbContext database)
    {
        try
        {
            // Basic connectivity check: attempt a lightweight query
            await database.Todos.AnyAsync();

            return Results.Ok(new { status = "ok", version = "1.0" });
        }
        catch
        {
            return Results.Json(new { status = "degraded" }, statusCode: 503);
        }
    }
}
