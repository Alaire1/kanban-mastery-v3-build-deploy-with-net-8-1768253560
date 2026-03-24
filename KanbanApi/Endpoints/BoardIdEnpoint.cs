using KanbanApi.Data;
using KanbanApi.Dtos;
using KanbanApi.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Endpoints;
//done
public static class BoardIdEndpoint
{
    public static IEndpointRouteBuilder MapBoardIdEndpoints(this IEndpointRouteBuilder routes)
    {
    
    var group = routes.MapGroup("/api/boards/{boardId}").RequireAuthorization();

    group.MapGet("/", async Task<IResult> (int boardId, ClaimsPrincipal user, IBoardService service) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return TypedResults.Unauthorized();

    return await service.GetBoardAsync(userId, boardId) switch
    {
        BoardResult.Found f       => TypedResults.Ok(f.Dto),
        BoardResult.NotFound nf   => TypedResults.NotFound(nf.Message),
        BoardResult.Forbidden     => TypedResults.Forbid(),
        _                         => TypedResults.StatusCode(500)
    };
    });
        return routes;
    }
}