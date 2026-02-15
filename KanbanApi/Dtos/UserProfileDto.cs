namespace KanbanApi.Dtos;

//immutable record for user profile response
public record UserProfileResponseDto
{
    public required string Id { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; } //optional

}

public record UserProfileUpdateDto
{
    public string? UserName { get; init; }
    public string? Email { get; init; }
}