using KanbanApi.Data;
using KanbanApi.Models;
using KanbanApi.Services;
using KanbanApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults; 
namespace KanbanApi.Endpoints;

public static class BoardMembersEndpoint
{
    public static IEndpointRouteBuilder MapBoardMembersEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/boards/{boardId}/members").RequireAuthorization();

        group.MapPost("/", async Task<IResult> (
            int boardId,
            AddMemberRequest request,
            HttpContext httpContext,
            ApplicationDbContext db,
            IAuthorizationService authService) =>
        {
            // 1. Check if board exists
            var boardExists = await db.Boards.AnyAsync(b => b.Id == boardId);
            if (!boardExists)
                return TypedResults.NotFound("Board not found.");

            // 2. Authorize user with IAuthorizationService + IsBoardOwnerRequirement
            var authResult = await authService.AuthorizeAsync(
                httpContext.User, boardId, new IsBoardOwnerRequirement());

            if (!authResult.Succeeded)
                return TypedResults.Forbid();

            // 3. Check if user is already a member

            var alreadyMember = await db.BoardMembers
                .AnyAsync(m => m.BoardId == boardId && m.UserId == request.UserId);
            if (alreadyMember)
                return TypedResults.Conflict("User is already a member of this board.");

            
            var member = new BoardMember(request.UserId, boardId, "Member");
            db.BoardMembers.Add(member);
            await db.SaveChangesAsync();

            var response = new BoardMemberDto
            {
                UserId = member.UserId,
                BoardId = member.BoardId.ToString(),
                Role = member.Role
            };
            
            return TypedResults.Created($"/api/boards/{boardId}/members/{member.UserId}", response);
        });

        return routes;
    }
}

public record AddMemberRequest(string UserId, string? Role);