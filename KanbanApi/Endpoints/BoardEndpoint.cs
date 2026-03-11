using System.Security.Claims;
using KanbanApi.Dtos;
using KanbanApi.Models;
using KanbanApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace KanbanApi.Endpoints;

public static class BoardEndpoint
{
    public static IEndpointRouteBuilder MapBoardEnpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/boards").RequireAuthorization();

        group.MapPost("/", async Task<IResult> (HttpContext httpContext) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            var dto = await httpContext.Request.ReadFromJsonAsync<CreateBoardDto>();
            if (dto is null || string.IsNullOrWhiteSpace(dto.BoardName))
                return TypedResults.BadRequest("Board name cannot be empty.");
            var boardService = httpContext.RequestServices.GetRequiredService<IBoardService>();
            // Create the Board entity
            var board = await boardService.CreateBoardAsync(dto.BoardName, userId);

            // Response for frontend display 
            var boardDto = new BoardDto
            {
                Id = board.Id,
                Name = board.Name,
                OwnerId = board.OwnerId,
                Role = "Owner", // Creator is always an Owner at the beginning
                Members = board.Members.Select(m => m.UserId).ToList() // Extract user IDs from BoardMember entities
            };

            return TypedResults.Created($"/api/boards/{boardDto.Id}", boardDto);
    
        });

        return routes;
    }
}