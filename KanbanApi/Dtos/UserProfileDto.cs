namespace KanbanApi.Dtos;

// used in UserProfileEndpoint for returning user profile details
public record UserProfileResponseDto
{
    public required string Id { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; } //optional
    public string? ProfileImageUrl { get; init; }
    public List<BoardDto> Boards { get; set; } = new();
}
