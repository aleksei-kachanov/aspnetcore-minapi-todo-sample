using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WebMinRouteGroup.Data;

namespace WebMinRouteGroup;

public static class TodoEndpointsV1
{
    public static RouteGroupBuilder MapTodosApiV1(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllTodos);
        group.MapGet("/{id}", GetTodo);
        group.MapPost("/", CreateTodo)
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

        group.MapDelete("/{id}", DeleteTodo);

        return group;
    }

    // get all todos
    // <snippet_1>
    public static async Task<Ok<Todo[]>> GetAllTodos(TodoGroupDbContext database)
    {
        var todos = await database.Todos.ToArrayAsync();
        return TypedResults.Ok(todos);
    }
    // </snippet_1>

    // get todo by id
    public static async Task<Results<Ok<Todo>, NotFound>> GetTodo(int id, TodoGroupDbContext database)
    {
        var todo = await database.Todos.FindAsync(id);

        if (todo != null)
        {
            return TypedResults.Ok(todo);
        }

        return TypedResults.NotFound();
    }

    // create todo
    public static async Task<Created<Todo>> CreateTodo(TodoDto todo, TodoGroupDbContext database)
    {
        var now = DateTime.UtcNow;
        var newTodo = new Todo
        {
            Title = todo.Title,
            Description = todo.Description,
            IsDone = todo.IsDone,
            DueDate = todo.DueDate,
            Priority = todo.Priority,
            CreatedAt = now,
            UpdatedAt = now
        };

        await database.Todos.AddAsync(newTodo);
        await database.SaveChangesAsync();

        return TypedResults.Created($"/todos/v1/{newTodo.Id}", newTodo);
    }

    // update todo
    public static async Task<Results<Ok<Todo>, NotFound>> UpdateTodo(UpdateTodoDto todo, int id, TodoGroupDbContext database)
    {
        var existingTodo = await database.Todos.FindAsync(id);

        if (existingTodo != null)
        {
            existingTodo.Title = todo.Title;
            existingTodo.Description = todo.Description;
            existingTodo.IsDone = todo.IsDone;
            existingTodo.DueDate = todo.DueDate;
            existingTodo.Priority = todo.Priority;
            existingTodo.UpdatedAt = DateTime.UtcNow;

            await database.SaveChangesAsync();

            return TypedResults.Ok(existingTodo);
        }

        return TypedResults.NotFound();
    }

    // delete todo
    public static async Task<Results<NoContent, NotFound>> DeleteTodo(int id, TodoGroupDbContext database)
    {
        var todo = await database.Todos.FindAsync(id);

        if (todo != null)
        {
            database.Todos.Remove(todo);
            await database.SaveChangesAsync();

            return TypedResults.NoContent();
        }

        return TypedResults.NotFound();
    }
}
