
using KanbanApi.Dtos;
namespace KanbanApi.Services;

public abstract record BoardResult
{
    public record Found(BoardIdResultDto Dto) : BoardResult;
    public record NotFound(string Message) : BoardResult;
    public record Forbidden : BoardResult;
}