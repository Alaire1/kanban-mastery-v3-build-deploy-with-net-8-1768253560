using Microsoft.AspNetCore.Identity;

namespace KanbanApi.Models;

public class ApplicationUser : IdentityUser {
    public string FullName { get; set; } = string.Empty;
    public ICollection<BoardMember> BoardMemberships { get; set; } = new List<BoardMember>();
}

