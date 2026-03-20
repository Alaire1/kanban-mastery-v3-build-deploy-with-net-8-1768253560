using KanbanApi.Data;
using KanbanApi.Dtos;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Endpoints;

public static class BoardIdEndpoint
{
    public static IEndpointRouteBuilder MapBoardIdEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/boards/{boardId}").RequireAuthorization();

        group.MapGet("/", async Task<IResult> (
            int boardId,
            HttpContext httpContext,
            ApplicationDbContext db,
            IAuthorizationService authService) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            var board = await db.Boards
                .AsNoTracking()
                .Include(b => b.Columns)
                    .ThenInclude(c => c.Cards)
                .Include(b => b.Members)
                .AsSplitQuery()
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
                return TypedResults.NotFound("Board not found.");

            var authResult = await authService.AuthorizeAsync(httpContext.User, boardId, "IsBoardMember");
            if (!authResult.Succeeded)
                return TypedResults.Forbid();

            var currentUserMembership = board.Members.FirstOrDefault(m => m.UserId == userId);

            var response = new BoardIdResultDto
            {
                Id = board.Id,
                Name = board.Name,
                OwnerId = board.OwnerId,
                Role = board.OwnerId == userId ? "Owner" : currentUserMembership?.Role ?? "Member",
                Members = board.Members
                    .Select(m => new BoardMemberResultDto
                    {
                        UserId = m.UserId,
                        Role = m.Role
                    })
                    .ToList(),
                Columns = board.Columns
                    .OrderBy(c => c.Position)
                    .Select(c => new BoardColumnResultDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Position = c.Position,
                        Cards = c.Cards
                            .Select(card => new BoardCardResultDto
                            {
                                Id = card.Id,
                                Title = card.Title
                            })
                            .ToList()
                    })
                    .ToList()
            };

            return TypedResults.Ok(response);
        });
        

        return routes;
    }
}