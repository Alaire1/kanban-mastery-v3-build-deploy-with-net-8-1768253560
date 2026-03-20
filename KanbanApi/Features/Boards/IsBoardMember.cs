using System.Security.Claims; 
using KanbanApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

public class IsBoardMemberRequirement : IAuthorizationRequirement { }

public class IsBoardMemberHandler : AuthorizationHandler<IsBoardMemberRequirement, int>
{
    private readonly ApplicationDbContext _db;

    public IsBoardMemberHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IsBoardMemberRequirement requirement,
        int boardId)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var isMember = await _db.BoardMembers
            .AnyAsync(m => m.BoardId == boardId && m.UserId == userId);

        if (isMember)
            context.Succeed(requirement);
    }
}