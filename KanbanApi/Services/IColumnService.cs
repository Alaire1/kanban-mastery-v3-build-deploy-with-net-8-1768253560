using KanbanApi.Dtos;

namespace KanbanApi.Services;

public interface IColumnService
{
    Task<ColumnResult> CreateColumnAsync(string userId, int boardId, CreateColumnRequestDto dto);
    Task<ColumnResult> UpdateColumnAsync(string userId, int boardId, int columnId, UpdateColumnNameRequestDto dto);
    Task<ColumnResult> DeleteColumnAsync(string userId, int boardId, int columnId);
}