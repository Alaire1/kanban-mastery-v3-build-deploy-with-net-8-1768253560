using Microsoft.AspNetCore.Identity;

namespace KanbanApi.Models;

public class ApplicationUser : IdentityUser {
    public string FullName { get; set; } = string.Empty;
    public List<BoardMember> Boards { get; set; } = new();
}

