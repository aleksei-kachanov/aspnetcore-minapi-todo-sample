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

        group.MapPut("/{id}", UpdateTodo);
        group.MapDelete("/{id}", DeleteTodo);

        return group;
    }

    // get all todos
    public static async Task<Ok<List<Todo>>> GetAllTodos(ITodoService todoService)
    {
        var todos = await todoService.GetAll();
        return TypedResults.Ok(todos);
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
    public static async Task<Results<Created<Todo>, NotFound>> UpdateTodo(TodoDto todo, int id, ITodoService todoService)
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

            return TypedResults.Created($"/todos/v2/{existingTodo.Id}", existingTodo);
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
