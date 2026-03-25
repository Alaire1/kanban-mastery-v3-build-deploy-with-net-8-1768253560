using System.ComponentModel.DataAnnotations;
using KanbanApi.Validation;

public class CreateColumnRequestDto
{
    [Required(ErrorMessage = "Column name cannot be empty")]
    [NotEmptyOrWhitespace]
    [MinLength(2)]
    [MaxLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9]+( [a-zA-Z0-9]+)*$",
        ErrorMessage = "Column name may only contain letters and numbers, with single spaces between words.")]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Position must be a non-negative integer.")]
    public int Position { get; set; }
}

public class UpdateColumnNameRequestDto
{
    [Required(ErrorMessage = "Column name cannot be empty")]
    [NotEmptyOrWhitespace]
    [MinLength(2)]
    [MaxLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9]+( [a-zA-Z0-9]+)*$",
        ErrorMessage = "Column name may only contain letters and numbers, with single spaces between words.")]
    public string Name { get; set; } = string.Empty;
}