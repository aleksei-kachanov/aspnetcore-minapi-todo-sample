using Microsoft.EntityFrameworkCore;
using WebMinRouteGroup.Data;

namespace WebMinRouteGroup.Services;

public class TodoService : ITodoService
{
    private readonly TodoGroupDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TodoService> _logger;

    public TodoService(TodoGroupDbContext dbContext, IEmailService emailService, IConfiguration configuration, ILogger<TodoService> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async ValueTask<Todo?> Find(int id)
    {
        return await _dbContext.Todos.FindAsync(id);
    }

    public async Task<List<Todo>> GetAll()
    {
        return await _dbContext.Todos.ToListAsync();
    }

    public async Task Add(Todo todo)
    {
        await _dbContext.Todos.AddAsync(todo);

        if (await _dbContext.SaveChangesAsync() > 0)
        {
            var emailAddress = _configuration["EmailAddress"];
            if (emailAddress is null)
            {
                _logger.LogWarning("EmailAddress configuration is missing; skipping notification email.");
            }
            else
            {
                await _emailService.Send(emailAddress, $"New todo has been added: {todo.Title}");
            }
        }
    }

    public async Task Update(Todo todo)
    {
        _dbContext.Todos.Update(todo);
        await _dbContext.SaveChangesAsync();
    }

    public async Task Remove(Todo todo)
    {
        _dbContext.Todos.Remove(todo);
        await _dbContext.SaveChangesAsync();
    }

    public Task<List<Todo>> GetIncompleteTodos()
    {
        return _dbContext.Todos.Where(t => t.IsDone == false).ToListAsync();
    }
}
