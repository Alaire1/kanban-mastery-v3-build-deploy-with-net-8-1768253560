using System.ComponentModel.DataAnnotations;

namespace KanbanApi.Dtos;

public class CreateCardRequestDto
{
    [Required]
    [RegularExpression(@"^[A-Za-z0-9]+( [A-Za-z0-9]+)*$", ErrorMessage = "Title must be alphanumeric with spaces only between words.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public int ColumnId { get; set; }
}