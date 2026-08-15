using WebMinRouteGroup.Data;

namespace WebMinRouteGroup;

public static class Utilities
{
    public static Dictionary<string, string[]> IsValid(TodoDto td)
        => ValidateTitle(td.Title);

    public static Dictionary<string, string[]> IsValid(UpdateTodoDto td)
        => ValidateTitle(td.Title);

    private static Dictionary<string, string[]> ValidateTitle(string title)
    {
        Dictionary<string, string[]> errors = new();

        if (string.IsNullOrEmpty(title))
        {
            errors.TryAdd("todo.name.errors", new[] { "Name is empty", "Name length < 3" });
            return errors;
        }

        if (title.Length < 3)
        {
            errors.TryAdd("todo.name.errors", new[] { "Name length < 3" });
        }

        return errors;
    }
}
