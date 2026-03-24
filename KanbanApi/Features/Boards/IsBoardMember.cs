using System.Security.Claims; 
using KanbanApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

public class IsBoardMemberRequirement : IAuthorizationRequirement { }

public class IsBoardMemberHandler : AuthorizationHandler<IsBoardMemberRequirement>
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IsBoardMemberHandler(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IsBoardMemberRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null) return;

        if (!httpContext.GetRouteData().Values.TryGetValue("boardId", out var boardIdValue)
            || !int.TryParse(boardIdValue?.ToString(), out var boardId))
            return;

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return;

        var isMember = await _db.BoardMembers
            .AnyAsync(m => m.BoardId == boardId && m.UserId == userId);
        if (isMember) { context.Succeed(requirement); return; }

        var isOwner = await _db.Boards
            .AnyAsync(b => b.Id == boardId && b.OwnerId == userId);
        if (isOwner)
            context.Succeed(requirement);
    }
}