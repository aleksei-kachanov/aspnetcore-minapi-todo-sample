using Microsoft.AspNetCore.Http.HttpResults;
using WebMinRouteGroup.Data;
using WebMinRouteGroup.Services;

namespace WebMinRouteGroup;

public static class TodoEndpointsV2
{
    public static RouteGroupBuilder MapTodosApiV2(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllTodos);
        group.MapGet("/incompleted", GetAllIncompletedTodos);
        group.MapGet("/overdue", GetOverdueTodos);
        group.MapGet("/{id}", GetTodo);

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

    // get all todos with optional filter / sort / pagination
    public static async Task<Results<Ok<PagedResult<Todo>>, BadRequest<string>>> GetAllTodos(
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
            var result = await todoService.GetPaged(queryParams);
            return TypedResults.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public static async Task<Ok<List<Todo>>> GetAllIncompletedTodos(ITodoService todoService)
    {
        var todos = await todoService.GetIncompleteTodos();
        return TypedResults.Ok(todos);
    }

    // get overdue todos
    public static async Task<Ok<List<Todo>>> GetOverdueTodos(ITodoService todoService)
    {
        var todos = await todoService.GetOverdueTodos();
        return TypedResults.Ok(todos);
    }

    // get todo by id
    public static async Task<Results<Ok<Todo>, NotFound>> GetTodo(int id, ITodoService todoService)
    {
        var todo = await todoService.Find(id);

        if (todo != null)
        {
            return TypedResults.Ok(todo);
        }

        return TypedResults.NotFound();
    }

    // create todo
    public static async Task<Created<Todo>> CreateTodo(TodoDto todo, ITodoService todoService)
    {
        var newTodo = new Todo
        {
            Title = todo.Title,
            Description = todo.Description,
            IsDone = todo.IsDone,
            DueDate = todo.DueDate,
            Priority = todo.Priority
        };

        await todoService.Add(newTodo);

        return TypedResults.Created($"/todos/v2/{newTodo.Id}", newTodo);
    }

    // update todo
    public static async Task<Results<Ok<Todo>, NotFound>> UpdateTodo(UpdateTodoDto todo, int id, ITodoService todoService)
    {
        var existingTodo = await todoService.Find(id);

        if (existingTodo != null)
        {
            existingTodo.Title = todo.Title;
            existingTodo.Description = todo.Description;
            existingTodo.IsDone = todo.IsDone;
            existingTodo.DueDate = todo.DueDate;
            existingTodo.Priority = todo.Priority;

            await todoService.Update(existingTodo);

            return TypedResults.Ok(existingTodo);
        }

        return TypedResults.NotFound();
    }

    // delete todo
    public static async Task<Results<NoContent, NotFound>> DeleteTodo(int id, ITodoService todoService)
    {
        var todo = await todoService.Find(id);

        if (todo != null)
        {
            await todoService.Remove(todo);
            return TypedResults.NoContent();
        }

        return TypedResults.NotFound();
    }
}
