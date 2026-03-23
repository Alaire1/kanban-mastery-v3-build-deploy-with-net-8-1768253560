
using System.ComponentModel.DataAnnotations;
namespace KanbanApi.Dtos;

// used in BoardEndpoint for returning board details along with members and their roles
public record BoardDto
{
    public required int Id { get; init; }
    public required string Name { get; init; } = string.Empty;
    public required string OwnerId { get; init; } = string.Empty;
    public required string Role { get; init; } = string.Empty; // Role of the user in the board
    public List<string> Members { get; init; } = new(); // List of user IDs or names
}

//used in BoardEndpoint for creating a new board
public class CreateBoardDto
{
    [Required]
    [MinLength(2)]
    [MaxLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9]+( [a-zA-Z0-9]+)*$",
        ErrorMessage = "Board name may only contain letters and numbers, with single spaces between words.")]
    public string BoardName { get; set; } = string.Empty;
}

public class UpdateBoardNameDto
{
    [Required]
    [MinLength(2)]
    [MaxLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9]+( [a-zA-Z0-9]+)*$",
        ErrorMessage = "Board name may only contain letters and numbers, with single spaces between words.")]
    public string Name { get; set; } = string.Empty;
}

//not used yet
// public record UpdateBoardOwnerDto
// {
//     public required string OwnerId { get; init; } = string.Empty;
// }

//used in BoardMembersEndpoint for returning member details when a new member is added to a board
public record BoardMemberDto
{
    public required string UserId { get; init; } = string.Empty;
    public required string BoardId { get; init; } = string.Empty;
    public required string Role { get; init; } = string.Empty;
}