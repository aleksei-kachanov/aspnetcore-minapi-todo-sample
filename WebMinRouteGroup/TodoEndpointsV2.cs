using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using WebMinRouteGroup.Data;
using WebMinRouteGroup.Services;

namespace WebMinRouteGroup;

public static class TodoEndpointsV2
{
    public static RouteGroupBuilder MapTodosApiV2(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllTodos).RequireAuthorization();
        group.MapGet("/incompleted", GetAllIncompletedTodos).RequireAuthorization();
        group.MapGet("/overdue", GetOverdueTodos).RequireAuthorization();
        group.MapGet("/{id}", GetTodo).RequireAuthorization();

        group.MapPost("/", CreateTodo)
            .RequireAuthorization()
            .AddEndpointFilter(async (efiContext, next) =>
            {
                var param = efiContext.GetArgument<TodoDto>(0);

                var validationErrors = Utilities.IsValid(param);

                if (validationErrors.Any())
                {
                    return Results.ValidationProblem(validationErrors);
                }

                return await next(efiContext);
            });

        group.MapPut("/{id}", UpdateTodo)
            .RequireAuthorization()
            .AddEndpointFilter(async (efiContext, next) =>
            {
                var param = efiContext.GetArgument<UpdateTodoDto>(0);

                var validationErrors = Utilities.IsValid(param);

                if (validationErrors.Any())
                {
                    return Results.ValidationProblem(validationErrors);
                }

                return await next(efiContext);
            });

        group.MapDelete("/{id}", DeleteTodo)
            .RequireAuthorization();

        return group;
    }

    // get all todos with optional filter / sort / pagination — scoped to authenticated user
    public static async Task<Results<Ok<PagedResult<Todo>>, BadRequest<string>, ForbidHttpResult>> GetAllTodos(
        ClaimsPrincipal user,
        ITodoService todoService,
        bool? isDone = null,
        string? priority = null,
        DateTime? dueBefore = null,
        DateTime? dueAfter = null,
        string? sortBy = null,
        string? order = null,
        int page = 1,
        int size = 20)
    {
        var ownerId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(ownerId))
        {
            return TypedResults.Forbid();
        }

        // Validate sortBy
        if (!string.IsNullOrEmpty(sortBy))
        {
            var validSortFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "dueDate", "priority", "createdAt", "title"
            };
            if (!validSortFields.Contains(sortBy))
            {
                return TypedResults.BadRequest($"Invalid sortBy value '{sortBy}'. Must be one of: dueDate, priority, createdAt, title.");
            }
        }

        // Validate order
        if (!string.IsNullOrEmpty(order) &&
            !string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.BadRequest($"Invalid order value '{order}'. Must be 'asc' or 'desc'.");
        }

        // Validate page/size
        if (page < 1)
        {
            return TypedResults.BadRequest("Page must be >= 1.");
        }

        if (size < 1 || size > 100)
        {
            return TypedResults.BadRequest("Size must be between 1 and 100.");
        }

        var queryParams = new TodoQueryParams
        {
            IsDone = isDone,
            Priority = priority,
            DueBefore = dueBefore,
            DueAfter = dueAfter,
            SortBy = sortBy,
            Order = order,
            Page = page,
            Size = size
        };

        try
        {
            var result = await todoService.GetPaged(queryParams, ownerId);
            return TypedResults.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public static async Task<Results<Ok<List<Todo>>, ForbidHttpResult>> GetAllIncompletedTodos(
        ClaimsPrincipal user,
        ITodoService todoService)
    {
        var ownerId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(ownerId))
        {
            return TypedResults.Forbid();
        }

        var todos = await todoService.GetIncompleteTodos(ownerId);
        return TypedResults.Ok(todos);
    }

    // get overdue todos
    public static async Task<Results<Ok<List<Todo>>, ForbidHttpResult>> GetOverdueTodos(
        ClaimsPrincipal user,
        ITodoService todoService)
    {
        var ownerId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(ownerId))
        {
            return TypedResults.Forbid();
        }

        var todos = await todoService.GetOverdueTodos(ownerId);
        return TypedResults.Ok(todos);
    }

    // get todo by id — returns 403 if owned by a different user
    public static async Task<Results<Ok<Todo>, NotFound, ForbidHttpResult>> GetTodo(
        int id,
        ClaimsPrincipal user,
        ITodoService todoService)
    {
        var ownerId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(ownerId))
        {
            return TypedResults.Forbid();
        }

        var todo = await todoService.Find(id);

        if (todo is null)
        {
            return TypedResults.NotFound();
        }

        if (todo.OwnerId != ownerId)
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Ok(todo);
    }

    // create todo — sets OwnerId from authenticated user's sub claim
    public static async Task<Results<Created<Todo>, ForbidHttpResult>> CreateTodo(
        TodoDto todo,
        ClaimsPrincipal user,
        ITodoService todoService)
    {
        var ownerId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(ownerId))
        {
            return TypedResults.Forbid();
        }

        var newTodo = new Todo
        {
            Title = todo.Title,
            Description = todo.Description,
            IsDone = todo.IsDone,
            DueDate = todo.DueDate,
            Priority = todo.Priority,
            OwnerId = ownerId
        };

        await todoService.Add(newTodo);

        return TypedResults.Created($"/todos/v2/{newTodo.Id}", newTodo);
    }

    // update todo — returns 403 if owned by a different user
    public static async Task<Results<Ok<Todo>, NotFound, ForbidHttpResult>> UpdateTodo(
        UpdateTodoDto todo,
        int id,
        ClaimsPrincipal user,
        ITodoService todoService)
    {
        var ownerId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(ownerId))
        {
            return TypedResults.Forbid();
        }

        var existingTodo = await todoService.Find(id);

        if (existingTodo is null)
        {
            return TypedResults.NotFound();
        }

        if (existingTodo.OwnerId != ownerId)
        {
            return TypedResults.Forbid();
        }

        existingTodo.Title = todo.Title;
        existingTodo.Description = todo.Description;
        existingTodo.IsDone = todo.IsDone;
        existingTodo.DueDate = todo.DueDate;
        existingTodo.Priority = todo.Priority;

        await todoService.Update(existingTodo);

        return TypedResults.Ok(existingTodo);
    }

    // delete todo — returns 403 if owned by a different user
    public static async Task<Results<NoContent, NotFound, ForbidHttpResult>> DeleteTodo(
        int id,
        ClaimsPrincipal user,
        ITodoService todoService)
    {
        var ownerId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(ownerId))
        {
            return TypedResults.Forbid();
        }

        var todo = await todoService.Find(id);

        if (todo is null)
        {
            return TypedResults.NotFound();
        }

        if (todo.OwnerId != ownerId)
        {
            return TypedResults.Forbid();
        }

        await todoService.Remove(todo);
        return TypedResults.NoContent();
    }
}
