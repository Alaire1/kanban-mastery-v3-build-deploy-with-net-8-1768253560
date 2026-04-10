using KanbanApi.Dtos;

namespace KanbanApi.Services;

public abstract record AddMemberResult
{
    public record Created(BoardMemberDto Dto) : AddMemberResult;
    public record BoardNotFound : AddMemberResult;
    public record UserNotFound : AddMemberResult;
    public record Forbidden : AddMemberResult;
    public record AlreadyMember : AddMemberResult;
}