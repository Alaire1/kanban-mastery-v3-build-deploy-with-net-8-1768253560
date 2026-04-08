namespace KanbanApi.Dtos;

public record AddMemberRequest
{
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public string? Role { get; init; }
}