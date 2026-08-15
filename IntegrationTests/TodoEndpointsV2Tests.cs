using System.Net;
using System.Net.Http.Json;
using IntegrationTests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WebMinRouteGroup.Data;
using WebMinRouteGroup.Services;
using System.Text.Json;

namespace IntegrationTests;

[Collection("Sequential")]
public class TodoEndpointsV2Tests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _httpClient;

    public TodoEndpointsV2Tests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    public static IEnumerable<object[]> InvalidTodos => new List<object[]>
    {
        new object[] { new TodoDto { Title = "", Description = "Test description", IsDone = false }, "Name is empty" },
        new object[] { new TodoDto { Title = "no", Description = "Test description", IsDone = false }, "Name length < 3" }
    };

    [Theory]
    [MemberData(nameof(InvalidTodos))]
    public async Task PostTodoWithValidationProblems(TodoDto todo, string errorMessage)
    {
        var response = await _httpClient.PostAsJsonAsync("/todos/v2", todo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problemResult = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problemResult?.Errors);
        Assert.Collection(problemResult.Errors, (error) => Assert.Equal(errorMessage, error.Value.First()));
    }

    [Fact]
    public async Task PostTodoWithValidParameters()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetService<TodoGroupDbContext>();
            if (db != null && db.Todos.Any())
            {
                db.Todos.RemoveRange(db.Todos);
                await db.SaveChangesAsync();
            }
        }

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IEmailService, TestEmailService>();
            });
        }).CreateClient();

        var response = await client.PostAsJsonAsync("/todos/v2", new TodoDto
        {
            Title = "Test title",
            Description = "Test description",
            IsDone = false
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var pagedResult = await client.GetFromJsonAsync<PagedResult<Todo>>("/todos/v2",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(pagedResult);
        Assert.NotNull(pagedResult.Items);
        Assert.Single(pagedResult.Items);

        Assert.Collection(pagedResult.Items, (todo) =>
        {
            Assert.Equal("Test title", todo.Title);
            Assert.Equal("Test description", todo.Description);
            Assert.False(todo.IsDone);
        });
    }

    [Fact]
    public async Task PostTodo_Unauthenticated_Returns401()
    {
        // Use a client that does not have the TestAuthHandler — real JWT bearer
        // will reject the request since no token is provided.
        var unauthenticatedClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Remove the test auth scheme and restore JWT Bearer so unauthenticated
                // requests get a proper 401 from the real bearer challenge.
                services.AddAuthentication(defaultScheme: "NoBearerTest")
                    .AddScheme<AuthenticationSchemeOptions, UnauthenticatedTestAuthHandler>(
                        "NoBearerTest", _ => { });
            });
        }).CreateClient();

        var response = await unauthenticatedClient.PostAsJsonAsync("/todos/v2", new TodoDto
        {
            Title = "Should be rejected",
            Description = "No token",
            IsDone = false
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostTodo_Authenticated_Returns201()
    {
        // The default factory client uses TestAuthHandler, so it is always authenticated.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetService<TodoGroupDbContext>();
            if (db != null && db.Todos.Any())
            {
                db.Todos.RemoveRange(db.Todos);
                await db.SaveChangesAsync();
            }
        }

        var response = await _httpClient.PostAsJsonAsync("/todos/v2", new TodoDto
        {
            Title = "Authenticated todo",
            Description = "Created with auth",
            IsDone = false
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
