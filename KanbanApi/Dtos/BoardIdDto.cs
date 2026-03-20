namespace KanbanApi.Dtos;


public record BoardIdResultDto
{
    public required int Id { get; init; }
    public required string Name { get; init; } = string.Empty;
    public required string OwnerId { get; init; } = string.Empty;
    public required string Role { get; init; } = string.Empty;

    public List<BoardMemberResultDto> Members { get; init; } = new();
    public List<BoardColumnResultDto> Columns { get; init; } = new();
}

public record BoardMemberResultDto
{
    public required string UserId { get; init; } = string.Empty;
    public required string Role { get; init; } = string.Empty;
}

public record BoardColumnResultDto
{
    public required int Id { get; init; }
    public required string Name { get; init; } = string.Empty;
    public required int Position { get; init; }

    public List<BoardCardResultDto> Cards { get; init; } = new();
}

public record BoardCardResultDto
{
    public required int Id { get; init; }
    public required string Title { get; init; } = string.Empty;
}