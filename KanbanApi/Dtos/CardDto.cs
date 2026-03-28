namespace KanbanApi.Dtos;
public record CardResponseDto
{
	public required int Id { get; init; }
	public required string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public required int ColumnId { get; init; }
}

public record CreateCardDto
{
	public required string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public required int ColumnId { get; init; }
}

public record UpdateCardDto
{
	public required string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public required int ColumnId { get; init; }
}

public record AssignCardResponseDto
{
	public required int CardId { get; init; }
	public required string UserId { get; init; }
}

