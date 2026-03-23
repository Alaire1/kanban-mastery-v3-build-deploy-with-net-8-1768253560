using System.Security.Claims;
using KanbanApi.Dtos;
using KanbanApi.Filters;
using KanbanApi.Services;

namespace KanbanApi.Endpoints;

public static class BoardMembersEndpoint
{
    public static IEndpointRouteBuilder MapBoardMembersEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/boards/{boardId}/members").RequireAuthorization();

        group.MapPost("/", async Task<IResult> (
            int boardId,
            AddMemberRequest request,
            ClaimsPrincipal user,
            IBoardMembersService service) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            return await service.AddMemberAsync(boardId, userId, request.UserId) switch
            {
                AddMemberResult.Created c       => TypedResults.Created($"/api/boards/{boardId}/members/{c.Dto.UserId}", c.Dto),
                AddMemberResult.BoardNotFound   => TypedResults.NotFound("Board not found."),
                AddMemberResult.Forbidden       => TypedResults.Forbid(),
                AddMemberResult.AlreadyMember   => TypedResults.Conflict("User is already a member of this board."),
                _                               => TypedResults.StatusCode(500)
            };
        })
        .WithValidation<AddMemberRequest>();

        return routes;
    }
}