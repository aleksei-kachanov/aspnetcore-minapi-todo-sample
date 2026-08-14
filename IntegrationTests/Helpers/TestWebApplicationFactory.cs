using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
            config.AddInMemoryCollection(new Dictionary<string, string?> { { "EmailAddress", "test1@Contoso.com" } });
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

            // Ensure schema is created before any test request hits the DB.
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TodoGroupDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
