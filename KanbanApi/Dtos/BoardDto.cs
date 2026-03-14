namespace KanbanApi.Dtos;

public class BoardDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // Role of the user in the board
    public List<string> Members { get; set; } = new(); // List of user IDs or names
}

public class CreateBoardDto
{
    public string BoardName { get; set; } = string.Empty;
}

public class UpdateBoardDto
{
    public string Name { get; set; } = string.Empty;
}