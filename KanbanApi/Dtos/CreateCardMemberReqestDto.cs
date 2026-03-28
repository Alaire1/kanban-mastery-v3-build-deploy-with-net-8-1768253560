
using System.ComponentModel.DataAnnotations;

namespace KanbanApi.Dtos;

public record AssignCardRequestDto
{
    [Required]
    [MinLength(1, ErrorMessage = "UserId cannot be empty.")]
    public required string UserId { get; init; }
}