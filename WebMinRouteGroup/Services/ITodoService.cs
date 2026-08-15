using WebMinRouteGroup.Data;

namespace WebMinRouteGroup.Services;

public interface ITodoService
{
    Task<List<Todo>> GetAll(string ownerId);

    Task<List<Todo>> GetIncompleteTodos(string ownerId);

    Task<List<Todo>> GetOverdueTodos(string ownerId);

    Task<PagedResult<Todo>> GetPaged(TodoQueryParams queryParams, string ownerId);

    ValueTask<Todo?> Find(int id);

    Task Add(Todo todo);

    Task Update(Todo todo);

    Task Remove(Todo todo);
}
