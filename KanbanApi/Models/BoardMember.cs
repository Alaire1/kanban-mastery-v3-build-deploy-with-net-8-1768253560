namespace KanbanApi.Models;


public class BoardMember{

    public string Id { get; set; }           
    
    public int BoardId { get; set; }

    public string UserId {get; set; } = string.Empty;
    
    public string Role {get; set;} = "Member";

    public Board Board { get; set; } = null!;

    public ApplicationUser User {get; set;} = null!;

}