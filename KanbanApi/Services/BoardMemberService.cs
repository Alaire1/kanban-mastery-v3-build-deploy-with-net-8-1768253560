using System.Security.Claims;
using KanbanApi.Data;
using KanbanApi.Dtos;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

public class BoardMembersService(ApplicationDbContext context) : IBoardMembersService
{
    public async Task<AddMemberResult> AddMemberAsync(int boardId, string requestingUserId, string targetUserId)
    {
        var board = await context.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null)
            return new AddMemberResult.BoardNotFound();

        if (board.OwnerId != requestingUserId)
            return new AddMemberResult.Forbidden();

        var alreadyMember = board.Members.Any(m => m.UserId == targetUserId);
        if (alreadyMember)
            return new AddMemberResult.AlreadyMember();

        var member = new BoardMember(targetUserId, boardId, "Member");
        context.BoardMembers.Add(member);
        await context.SaveChangesAsync();

        return new AddMemberResult.Created(new BoardMemberDto
        {
            UserId = member.UserId,
            BoardId = member.BoardId.ToString(),
            Role = member.Role
        });
    }
}