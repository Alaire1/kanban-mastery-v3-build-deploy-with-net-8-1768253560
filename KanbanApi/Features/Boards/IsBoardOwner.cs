using System.Security.Claims;
using KanbanApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

// The requirement — just a marker class
public class IsBoardOwnerRequirement : IAuthorizationRequirement { }

// The handler — the framework calls this when you authorize against IsBoardOwnerRequirement
public class IsBoardOwnerHandler : AuthorizationHandler<IsBoardOwnerRequirement>
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IsBoardOwnerHandler(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IsBoardOwnerRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null) return;

        // Read boardId directly from the route
        if (!httpContext.GetRouteData().Values.TryGetValue("boardId", out var boardIdValue)
            || !int.TryParse(boardIdValue?.ToString(), out var boardId))
            return;

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return;

        var isOwner = await _db.BoardMembers
            .AnyAsync(m => m.BoardId == boardId && m.UserId == userId && m.Role == "Owner");
        if (isOwner) { context.Succeed(requirement); return; }

        var ownerFlag = await _db.Boards
            .AnyAsync(b => b.Id == boardId && b.OwnerId == userId);
        if (ownerFlag) context.Succeed(requirement);
    }
}