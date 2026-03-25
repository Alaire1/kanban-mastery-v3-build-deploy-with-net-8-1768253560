using KanbanApi.Data;
using KanbanApi.Dtos;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

public class ColumnService(ApplicationDbContext context) : IColumnService
{
    public async Task<ColumnResult> CreateColumnAsync(string userId, int boardId, CreateColumnRequestDto dto)
    {
        var board = await context.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null)
            return new ColumnResult.BoardNotFound();

        var isMember = board.OwnerId == userId || board.Members.Any(m => m.UserId == userId);
        if (!isMember)
            return new ColumnResult.Forbidden();

        var positionTaken = await context.Columns
            .AnyAsync(c => c.BoardId == boardId && c.Position == dto.Position);
        if (positionTaken)
            return new ColumnResult.PositionTaken();

        var column = new Column(dto.Name, dto.Position, board);
        context.Columns.Add(column);
        await context.SaveChangesAsync();

        return new ColumnResult.Created(new ColumnApiResponseDto
        {
            Id = column.Id,
            Name = column.Name,
            Position = column.Position
        });
    }

    public async Task<ColumnResult> UpdateColumnAsync(string userId, int boardId, int columnId, UpdateColumnNameRequestDto dto)
    {
        var board = await context.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null)
            return new ColumnResult.BoardNotFound();

        var isMember = board.OwnerId == userId || board.Members.Any(m => m.UserId == userId);
        if (!isMember)
            return new ColumnResult.Forbidden();

        var column = await context.Columns
            .FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId);
        if (column is null)
            return new ColumnResult.ColumnNotFound();

        column.Rename(dto.Name);
        await context.SaveChangesAsync();

        return new ColumnResult.Updated(new ColumnApiResponseDto
        {
            Id = column.Id,
            Name = column.Name,
            Position = column.Position
        });
    }

    public async Task<ColumnResult> DeleteColumnAsync(string userId, int boardId, int columnId)
    {
        var board = await context.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null)
            return new ColumnResult.BoardNotFound();

        var isMember = board.OwnerId == userId || board.Members.Any(m => m.UserId == userId);
        if (!isMember)
            return new ColumnResult.Forbidden();

        var column = await context.Columns
            .FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId);
        if (column is null)
            return new ColumnResult.ColumnNotFound();

        var hasCards = await context.Cards.AnyAsync(c => c.ColumnId == columnId);
        if (hasCards)
            return new ColumnResult.HasCards();

        context.Columns.Remove(column);
        await context.SaveChangesAsync();

        return new ColumnResult.Deleted();
    }
}