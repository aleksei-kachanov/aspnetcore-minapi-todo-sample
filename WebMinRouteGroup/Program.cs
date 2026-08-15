using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using WebMinRouteGroup;
using WebMinRouteGroup.Data;
using WebMinRouteGroup.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddTransient<ITodoService, TodoService>();
builder.Services.AddSingleton<IEmailService, EmailService>();


builder.Services.AddDbContext<TodoGroupDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlite(connectionString);
});

var app = builder.Build();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetService<TodoGroupDbContext>();
// Only run migrations for real (non-in-memory) databases.
// In-memory SQLite used by integration tests creates its own schema via EnsureCreated.
if (db != null && db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
    && !db.Database.GetConnectionString()!.Contains(":memory:"))
{
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar");
}

// health check endpoint
app.MapGroup("")
    .MapHealthApi()
    .WithTags("Health");

// todoV1 endpoints
app.MapGroup("/todos/v1")
    .MapTodosApiV1()
    .WithTags("Todo Endpoints");

// todoV2 endpoints
app.MapGroup("/todos/v2")
    .MapTodosApiV2()
    .WithTags("Todo Endpoints");

app.Run();

public partial class Program
{ }
