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

            services.AddDbContext<TodoGroupDbContext>(options =>
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                options.UseSqlite($"Data Source={Path.Join(path, "WebMinRouteGroup_tests.db")}");
            });
        });
    }
}
