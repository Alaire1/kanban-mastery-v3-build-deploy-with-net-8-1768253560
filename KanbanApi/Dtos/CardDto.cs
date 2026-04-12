using System.ComponentModel.DataAnnotations;

namespace KanbanApi.Dtos;
public record CardResponseDto
{
	public required int Id { get; init; }
	public required string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public required int ColumnId { get; init; }
}

public record UpdateCardDto
{
	[Required]
	[RegularExpression(@"^[A-Za-z0-9]+( [A-Za-z0-9]+)*$", ErrorMessage = "Title must be alphanumeric with spaces only between words.")]
	public required string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	[Required]
	[Range(1, int.MaxValue, ErrorMessage = "ColumnId must be a positive integer.")]
	public required int ColumnId { get; init; }
}

public record AssignCardResponseDto
{
	public required int CardId { get; init; }
	public required string UserId { get; init; }
}

