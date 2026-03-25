namespace KanbanApi.Dtos;

public record ColumnApiResponseDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required int Position { get; init; }
}