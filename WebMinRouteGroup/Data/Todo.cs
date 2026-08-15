using System.ComponentModel.DataAnnotations;

namespace WebMinRouteGroup.Data;

public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2
}

public class Todo
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    public bool IsDone { get; set; }
    public DateTime? DueDate { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// The user ID (sub claim from JWT) who owns this todo.
    /// </summary>
    [MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;
}
