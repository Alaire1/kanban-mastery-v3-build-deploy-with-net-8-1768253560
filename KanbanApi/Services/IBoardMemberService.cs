using KanbanApi.Dtos;

namespace KanbanApi.Services;

public interface IBoardMembersService
{
    Task<AddMemberResult> AddMemberAsync(int boardId, string requestingUserId, string targetUserId);
}