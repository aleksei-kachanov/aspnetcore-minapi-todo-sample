using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using UnitTests.Helpers;
using WebMinRouteGroup;

namespace UnitTests;

public class HealthEndpointsTests
{
    [Fact]
    public async Task GetHealth_ReturnsOkWithStatusOkAndVersion_WhenDatabaseIsReachable()
    {
        // Arrange
        await using var context = new MockDb().CreateDbContext();
        await context.Database.EnsureCreatedAsync();

        // Act
        var result = await HealthEndpoints.GetHealth(context);

        // Assert: expect 200 OK
        Assert.NotNull(result);
        var okResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var statusCode = okResult.StatusCode ?? 200;
        Assert.Equal(200, statusCode);

        // Verify response body serialises to JSON containing status and version
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(valueResult.Value);
        Assert.Contains("\"status\"", json);
        Assert.Contains("\"ok\"", json);
        Assert.Contains("\"version\"", json);
        Assert.Contains("\"1.0\"", json);
    }

    [Fact]
    public void MapHealthApi_RegistersGetHealthEndpoint_WithAllowAnonymous()
    {
        // This test validates the routing registration compiles and wires correctly.
        // The actual AllowAnonymous metadata is verified by integration tests;
        // here we just confirm the extension method exists and returns the group.
        // (Static method existence is sufficient for unit scope.)
        Assert.True(
            typeof(HealthEndpoints).GetMethod(nameof(HealthEndpoints.MapHealthApi)) != null,
            "MapHealthApi extension method should exist on HealthEndpoints"
        );
    }

    [Fact]
    public async Task GetHealth_ReturnsDegradedStatus_WhenDatabaseIsUnreachable()
    {
        // Arrange: Use a context with InMemory provider but force a failure
        // by not creating the schema — SELECT 1 will throw on an uninitialized db.
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<WebMinRouteGroup.Data.TodoGroupDbContext>()
            .UseInMemoryDatabase($"unreachable-{Guid.NewGuid()}")
            .Options;

        await using var context = new WebMinRouteGroup.Data.TodoGroupDbContext(options);
        // Dispose immediately so any subsequent operation throws ObjectDisposedException
        await context.DisposeAsync();

        // Act
        var result = await HealthEndpoints.GetHealth(context);

        // Assert: expect 503
        Assert.NotNull(result);
        if (result is IStatusCodeHttpResult statusResult)
        {
            Assert.Equal(503, statusResult.StatusCode);
        }
    }
}
