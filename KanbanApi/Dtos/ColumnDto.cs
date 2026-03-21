namespace KanbanApi.Dtos;

public record ColumnDto
{
    public required int Id { get; init; }
    public required string Name { get; init; } = string.Empty;
    public required int Position { get; init; }
}