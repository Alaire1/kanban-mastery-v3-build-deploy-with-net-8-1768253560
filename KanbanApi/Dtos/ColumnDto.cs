namespace KanbanApi.Dtos;

public record ColumnApiResponseDto
{
    public required int Id { get; init; }
    public required string Name { get; init; } = string.Empty;
    public required int Position { get; init; }
}

// Updated to match the style of the first ColumnDto

public record CreateColumnRequestDto
{
    public required string Name { get; set; }
    public required int Position { get; set; }
}

public record UpdateColumnNameRequestDto
{
    public required string Name { get; set; }
}

// public record UpdateColumnPositionRequestDto
// {
//     public required int Position { get; set; }
// }

