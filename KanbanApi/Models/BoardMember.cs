namespace KanbanApi.Models;

public class BoardMember
{
    public int BoardId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string Role { get; private set; } = "Member";
    
    public Board Board { get; private set; } = null!;
    public ApplicationUser User { get; private set; } = null!;

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