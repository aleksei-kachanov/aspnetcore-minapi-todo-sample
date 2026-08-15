using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebMinRouteGroup.Data;

namespace IntegrationTests.Helpers;

public class TestWebApplicationFactory<TProgram>
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmailAddress", "test1@Contoso.com" },
                { "Jwt:Key", "test-secret-key-that-is-long-enough-for-hmac-sha256" },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" },
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TodoGroupDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Use a shared in-memory SQLite connection so the schema persists
            // for the lifetime of the test host. Without a shared connection,
            // each DbContext opens a new :memory: file that disappears immediately.
            var keepAliveConnection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            keepAliveConnection.Open();

            services.AddDbContext<TodoGroupDbContext>(options =>
            {
                options.UseSqlite(keepAliveConnection);
            });

            // Ensure schema is created for the in-memory test DB.
            // Program.cs startup skips MigrateAsync for :memory: connections,
            // so we create the schema here before any test request arrives.
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TodoGroupDbContext>();
            db.Database.EnsureCreated();
        });

        // Replace JWT bearer with a test auth handler so integration tests
        // don't need real tokens. All requests via the default client are
        // auto-authenticated as a test user.
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(defaultScheme: "Test")
                .AddScheme<TestAuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }
}
