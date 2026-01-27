namespace KanbanApi.Models;

public class BoardMember
{
    public string Id { get; set; } = string.Empty;
    public int BoardId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "Member";
    
    public Board Board { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    // Default constructor
    private BoardMember() { }

    // Parameterized constructor
    public BoardMember(string userId, int boardId, string role = "Member")
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty!", nameof(userId));
        
        UserId = userId;
        BoardId = boardId;
        Role = role;
    }
}