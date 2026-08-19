using System.ComponentModel.DataAnnotations;

namespace WebMinRouteGroup.Data;

public class TodoQueryParams
{
    /// <summary>Filter by completion status.</summary>
    public bool? IsDone { get; set; }

    /// <summary>Filter by priority (Low, Medium, High).</summary>
    public string? Priority { get; set; }

    /// <summary>Include tasks with DueDate on or before this date (ISO 8601).</summary>
    public DateTime? DueBefore { get; set; }

    /// <summary>Include tasks with DueDate on or after this date (ISO 8601).</summary>
    public DateTime? DueAfter { get; set; }

    /// <summary>Field to sort by: dueDate | priority | createdAt | title. Default: createdAt.</summary>
    public string? SortBy { get; set; }

    /// <summary>Sort direction: asc | desc. Default: desc (newest first for createdAt).</summary>
    public string? Order { get; set; }

    /// <summary>1-based page number. Default: 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Number of items per page. Default: 20. Maximum: 100.</summary>
    public int Size { get; set; } = 20;

    /// <summary>Full-text search term matched against Title (case-insensitive substring). Maximum: 200 characters.</summary>
    [MaxLength(200)]
    public string? Search { get; set; }
}
