using System.Security.Claims;
using KanbanApi.Dtos;
using KanbanApi.Filters;
using KanbanApi.Services;

namespace KanbanApi.Endpoints;

public static class BoardIdEndpoint
{
    public static IEndpointRouteBuilder MapBoardIdEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/boards/{boardId}").RequireAuthorization();

        group.MapGet("/", async Task<IResult> (
            int boardId,
            ClaimsPrincipal user,
            IBoardService service) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            return await service.GetBoardAsync(userId, boardId) switch
            {
                BoardResult.Found f     => TypedResults.Ok(f.Dto),
                BoardResult.NotFound nf => TypedResults.NotFound(nf.Message),
                BoardResult.Forbidden   => TypedResults.Forbid(),
                _                       => TypedResults.StatusCode(500)
            };
        })
        .RequireAuthorization("IsBoardMember");


        group.MapPut("/", async Task<IResult> (
            int boardId,
            UpdateBoardNameDto dto,
            ClaimsPrincipal user,
            IBoardService service) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            return await service.UpdateBoardAsync(userId, boardId, dto.Name) switch
            {
                BoardResult.Updated u   => TypedResults.Ok(u.Dto),
                BoardResult.NotFound nf => TypedResults.NotFound(nf.Message),
                BoardResult.Forbidden   => TypedResults.Forbid(),
                _                       => TypedResults.StatusCode(500)
            };
        })
        .WithValidation<UpdateBoardNameDto>()
        .RequireAuthorization("IsBoardOwner");

        group.MapDelete("/", async Task<IResult> (
            int boardId,
            ClaimsPrincipal user,
            IBoardService service) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            return await service.DeleteBoardAsync(userId, boardId) switch
            {
                BoardResult.Deleted     => TypedResults.NoContent(),
                BoardResult.NotFound nf => TypedResults.NotFound(nf.Message),
                BoardResult.Forbidden   => TypedResults.Forbid(),
                _                       => TypedResults.StatusCode(500)
            };
        })
        .RequireAuthorization("IsBoardOwner");

        return routes;
    }
}