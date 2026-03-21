using KanbanApi.Data;
using KanbanApi.Models;
using KanbanApi.Services;
using KanbanApi.Dtos;
using KanbanApi.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
namespace KanbanApi.Endpoints;

public static class ColumnsEndpoint
{
    public static IEndpointRouteBuilder MapColumnsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/boards/{boardId}/columns").RequireAuthorization();
        
        group.MapPost("/", async Task<IResult> (
            int boardId,
            CreateColumnRequest request,
            HttpContext httpContext,
            ApplicationDbContext db,
            IAuthorizationService authService) =>
        {
            // Board check
            var board = await db.Boards.FindAsync(boardId);
            if (board == null)
                return TypedResults.NotFound("Board not found.");
            
            // Autorize user 
            var authResult = await authService.AuthorizeAsync(
                httpContext.User, boardId, new IsBoardMemberRequirement());
            if (!authResult.Succeeded)
                return TypedResults.Forbid();

            //Validate name 
            if (!NameValidator.TryValidateAndNormalize(request.Name, out var normalizedName, out var error))
                return TypedResults.BadRequest(error);
                
            // Validate position
            if (request.Position < 0)
                return TypedResults.BadRequest("Position must be a non-negative integer.");

            var positionTaken = await db.Columns
                .AnyAsync(c => c.BoardId == boardId && c.Position == request.Position);
            if (positionTaken)
                return TypedResults.Conflict("A column already exists at this position.");

            //Create column
            var column = new Column(normalizedName, request.Position, board);
            db.Columns.Add(column);
            await db.SaveChangesAsync();
            
            var response = new ColumnDto
            {
                Id = column.Id,
                Name = column.Name,
                Position = column.Position
            };

            return TypedResults.Created($"/api/boards/{boardId}/columns/{column.Id}", response);
        });

        group.MapPut("/{columnId:int}", async Task<IResult> (
            int boardId,
            int columnId,
            UpdateColumnRequest request,
            HttpContext httpContext,
            ApplicationDbContext db,
            IAuthorizationService authService) =>
        {
            // Board check
            var board = await db.Boards.FindAsync(boardId);
            if (board == null)
                return TypedResults.NotFound("Board not found.");

            // Autorize user
            var authResult = await authService.AuthorizeAsync(
                httpContext.User, boardId, new IsBoardMemberRequirement());
            if (!authResult.Succeeded)
                return TypedResults.Forbid();

            // Column check
            var column = await db.Columns
                .FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId);
            if (column == null)
                return TypedResults.NotFound("Column not found.");

            // Validate name
            if (!NameValidator.TryValidateAndNormalize(request.Name, out var normalizedName, out var error))
                return TypedResults.BadRequest(error);

            // Validate position
            if (request.Position < 0)
                return TypedResults.BadRequest("Position must be a non-negative integer.");
            var positionTaken = await db.Columns
                .AnyAsync(c => c.BoardId == boardId && c.Id != columnId && c.Position == request.Position);
            if (positionTaken)
                return TypedResults.Conflict("A column already exists at this position.");

            column.Rename(normalizedName);
            column.Reposition(request.Position);

            await db.SaveChangesAsync();

            var response = new ColumnDto
            {
                Id = column.Id,
                Name = column.Name,
                Position = column.Position
            };

            return TypedResults.Ok(response);
        });

        group.MapDelete("/{columnId:int}", async Task<IResult> (
            int boardId,
            int columnId,
            HttpContext httpContext,
            ApplicationDbContext db,
            IAuthorizationService authService) =>
        {
            // Board check
            var board = await db.Boards.FindAsync(boardId);
            if (board == null)
                return TypedResults.NotFound("Board not found.");

            // Autorize user
            var authResult = await authService.AuthorizeAsync(
                httpContext.User, boardId, new IsBoardMemberRequirement());
            if (!authResult.Succeeded)
                return TypedResults.Forbid();
            
            // Column check
            var column = await db.Columns
                .FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId);
            if (column == null)
                return TypedResults.NotFound("Column not found.");

            db.Columns.Remove(column);
            await db.SaveChangesAsync();

            return TypedResults.NoContent();
        });

        return routes;
    }
}

public record CreateColumnRequest(string Name, int Position);
public record UpdateColumnRequest(string Name, int Position);