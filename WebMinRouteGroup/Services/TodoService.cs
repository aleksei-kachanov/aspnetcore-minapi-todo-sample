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
        var now = DateTime.UtcNow;
        todo.CreatedAt = now;
        todo.UpdatedAt = now;

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
        todo.UpdatedAt = DateTime.UtcNow;
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

    public Task<List<Todo>> GetOverdueTodos()
    {
        var now = DateTime.UtcNow;
        return _dbContext.Todos
            .Where(t => t.DueDate < now && t.IsDone == false)
            .ToListAsync();
    }

    public async Task<PagedResult<Todo>> GetPaged(TodoQueryParams queryParams)
    {
        // Clamp page size: minimum 1, maximum 100
        var size = Math.Clamp(queryParams.Size, 1, 100);
        var page = Math.Max(queryParams.Page, 1);

        IQueryable<Todo> query = _dbContext.Todos;

        // --- Filtering ---
        if (queryParams.IsDone.HasValue)
        {
            query = query.Where(t => t.IsDone == queryParams.IsDone.Value);
        }

        if (!string.IsNullOrEmpty(queryParams.Priority))
        {
            if (!Enum.TryParse<Priority>(queryParams.Priority, ignoreCase: true, out var priorityValue))
            {
                throw new ArgumentException($"Invalid priority value '{queryParams.Priority}'. Must be one of: Low, Medium, High.");
            }
            query = query.Where(t => t.Priority == priorityValue);
        }

        if (queryParams.DueBefore.HasValue)
        {
            // Include tasks due on or before the given date (end of that day, UTC)
            var dueBefore = queryParams.DueBefore.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(t => t.DueDate <= dueBefore);
        }

        if (queryParams.DueAfter.HasValue)
        {
            // Include tasks due on or after the given date (start of that day, UTC)
            var dueAfter = queryParams.DueAfter.Value.Date;
            query = query.Where(t => t.DueDate >= dueAfter);
        }

        // --- Sorting ---
        var sortBy = queryParams.SortBy?.ToLowerInvariant();
        var descending = string.Equals(queryParams.Order, "desc", StringComparison.OrdinalIgnoreCase);

        query = sortBy switch
        {
            "duedate" => descending
                ? query.OrderByDescending(t => t.DueDate)
                : query.OrderBy(t => t.DueDate),
            "priority" => descending
                ? query.OrderByDescending(t => t.Priority)
                : query.OrderBy(t => t.Priority),
            "title" => descending
                ? query.OrderByDescending(t => t.Title)
                : query.OrderBy(t => t.Title),
            // Default: createdAt desc (newest first) when no sort specified
            _ when string.IsNullOrEmpty(sortBy) => query.OrderByDescending(t => t.CreatedAt),
            // "createdat" or any explicit value
            _ => descending
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt),
        };

        // --- Pagination ---
        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new PagedResult<Todo>
        {
            Items = items,
            Total = total,
            Page = page,
            Size = size
        };
    }
}
