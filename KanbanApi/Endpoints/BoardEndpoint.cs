using System.Security.Claims;
using KanbanApi.Dtos;
using KanbanApi.Filters;
using KanbanApi.Services;

namespace KanbanApi.Endpoints;
//done
public static class BoardEndpoint
{
    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/boards").RequireAuthorization();

        group.MapPost("/", async Task<IResult> (
            CreateBoardDto dto,
            ClaimsPrincipal user,
            IBoardService boardService) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            var boardDto = await boardService.CreateBoardAsync(dto.BoardName, userId);

            return TypedResults.Created($"/api/boards/{boardDto.Id}", boardDto);
        })
        .WithValidation<CreateBoardDto>();

        return routes;
    }
}