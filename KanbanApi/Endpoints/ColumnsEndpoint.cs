using System.Security.Claims;
using KanbanApi.Dtos;
using KanbanApi.Filters;
using KanbanApi.Services;

namespace KanbanApi.Endpoints;

public static class ColumnsEndpoint
{
    public static IEndpointRouteBuilder MapColumnsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/boards/{boardId}/columns").RequireAuthorization();

        group.MapPost("/", async Task<IResult> (
            int boardId,
            CreateColumnRequestDto dto,
            ClaimsPrincipal user,
            IColumnService service) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            return await service.CreateColumnAsync(userId, boardId, dto) switch
            {
                ColumnResult.Created c      => TypedResults.Created($"/api/boards/{boardId}/columns/{c.Dto.Id}", c.Dto),
                ColumnResult.BoardNotFound  => TypedResults.NotFound("Board not found."),
                ColumnResult.Forbidden      => TypedResults.Forbid(),
                ColumnResult.PositionTaken  => TypedResults.Conflict("A column already exists at this position."),
                _                           => TypedResults.StatusCode(500)
            };
        })
        .WithValidation<CreateColumnRequestDto>()
        .RequireAuthorization("IsBoardMember");

        group.MapPut("/{columnId}", async Task<IResult> (
            int boardId,
            int columnId,
            UpdateColumnNameRequestDto dto,
            ClaimsPrincipal user,
            IColumnService service) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            return await service.UpdateColumnAsync(userId, boardId, columnId, dto) switch
            {
                ColumnResult.Updated u      => TypedResults.Ok(u.Dto),
                ColumnResult.BoardNotFound  => TypedResults.NotFound("Board not found."),
                ColumnResult.ColumnNotFound => TypedResults.NotFound("Column not found."),
                ColumnResult.Forbidden      => TypedResults.Forbid(),
                _                           => TypedResults.StatusCode(500)
            };
        })
        .WithValidation<UpdateColumnNameRequestDto>()
        .RequireAuthorization("IsBoardMember");

        group.MapDelete("/{columnId}", async Task<IResult> (
            int boardId,
            int columnId,
            ClaimsPrincipal user,
            IColumnService service) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            return await service.DeleteColumnAsync(userId, boardId, columnId) switch
            {
                ColumnResult.Deleted        => TypedResults.NoContent(),
                ColumnResult.BoardNotFound  => TypedResults.NotFound("Board not found."),
                ColumnResult.ColumnNotFound => TypedResults.NotFound("Column not found."),
                ColumnResult.Forbidden      => TypedResults.Forbid(),
                ColumnResult.HasCards       => TypedResults.BadRequest("Cannot delete column with existing cards. Remove them first."),
                _                           => TypedResults.StatusCode(500)
            };
        })
        .RequireAuthorization("IsBoardMember");

        return routes;
    }
}