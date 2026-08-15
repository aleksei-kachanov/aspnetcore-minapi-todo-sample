using System.ComponentModel.DataAnnotations;

namespace WebMinRouteGroup.Data;

public class UpdateTodoDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    public bool IsDone { get; set; }
    public DateTime? DueDate { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
}
