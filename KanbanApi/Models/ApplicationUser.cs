using Microsoft.AspNetCore.Identity;

namespace KanbanApi.Models;

public class ApplicationUser : IdentityUser {
    public ICollection<BoardMember> BoardMemberships { get; private set; } = new List<BoardMember>();

    //Default constructor
    public ApplicationUser(){} //only for Identity

    // Parameterized constructor
    public ApplicationUser(string nickname){
        if (string.IsNullOrWhiteSpace(nickname))
            throw new ArgumentException("Nickname is required", nameof(nickname));
        
        UserName = nickname;
        BoardMemberships = new List<BoardMember>();
    }
}
