using KanbanApi.Dtos;

namespace KanbanApi.Services;

public enum CardResultType
{
    Success,
    CardNotFound,
    ColumnNotFound,
    Forbidden,
    BadRequest,
    UnknownError
}

public record CardResult
{
    public CardResultType ResultType { get; init; }
    public CardResponseDto? Dto { get; init; }
    public AssignCardResponseDto? AssignDto { get; init; }
    public string? Message { get; init; }

    public static CardResult Success(CardResponseDto? dto = null) => new() { ResultType = CardResultType.Success, Dto = dto };
    public static CardResult Success(AssignCardResponseDto? assignDto) => new() { ResultType = CardResultType.Success, AssignDto = assignDto };
    public static CardResult CardNotFound() => new() { ResultType = CardResultType.CardNotFound, Message = "Card not found." };
    public static CardResult ColumnNotFound() => new() { ResultType = CardResultType.ColumnNotFound, Message = "Column not found." };
    public static CardResult Forbidden() => new() { ResultType = CardResultType.Forbidden, Message = "Forbidden." };
    public static CardResult BadRequest(string? message = null) => new() { ResultType = CardResultType.BadRequest, Message = message ?? "Bad request." };
    public static CardResult UnknownError(string? message = null) => new() { ResultType = CardResultType.UnknownError, Message = message ?? "Unknown error." };
}