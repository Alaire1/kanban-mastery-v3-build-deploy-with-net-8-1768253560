using KanbanApi.Dtos;
using KanbanApi.Services;
using Xunit;

namespace KanbanApi.Tests.Services;

public class CardResultTests
{
    [Fact]
    public void Success_WithCardDto_SetsSuccessAndDto()
    {
        var dto = new CardResponseDto
        {
            Id = 1,
            Title = "Card",
            Description = "Desc",
            ColumnId = 10
        };

        var result = CardResult.Success(dto);

        Assert.Equal(CardResultType.Success, result.ResultType);
        Assert.Same(dto, result.Dto);
        Assert.Null(result.AssignDto);
    }

    [Fact]
    public void Success_WithAssignDto_SetsSuccessAndAssignDto()
    {
        var dto = new AssignCardResponseDto
        {
            CardId = 1,
            UserId = "user-1"
        };

        var result = CardResult.Success(dto);

        Assert.Equal(CardResultType.Success, result.ResultType);
        Assert.Same(dto, result.AssignDto);
        Assert.Null(result.Dto);
    }

    [Fact]
    public void CardNotFound_SetsExpectedTypeAndMessage()
    {
        var result = CardResult.CardNotFound();

        Assert.Equal(CardResultType.CardNotFound, result.ResultType);
        Assert.Equal("Card not found.", result.Message);
    }

    [Fact]
    public void ColumnNotFound_SetsExpectedTypeAndMessage()
    {
        var result = CardResult.ColumnNotFound();

        Assert.Equal(CardResultType.ColumnNotFound, result.ResultType);
        Assert.Equal("Column not found.", result.Message);
    }

    [Fact]
    public void Forbidden_SetsExpectedTypeAndMessage()
    {
        var result = CardResult.Forbidden();

        Assert.Equal(CardResultType.Forbidden, result.ResultType);
        Assert.Equal("Forbidden.", result.Message);
    }

    [Fact]
    public void BadRequest_WithoutMessage_UsesDefaultMessage()
    {
        var result = CardResult.BadRequest();

        Assert.Equal(CardResultType.BadRequest, result.ResultType);
        Assert.Equal("Bad request.", result.Message);
    }

    [Fact]
    public void UnknownError_WithoutMessage_UsesDefaultMessage()
    {
        var result = CardResult.UnknownError();

        Assert.Equal(CardResultType.UnknownError, result.ResultType);
        Assert.Equal("Unknown error.", result.Message);
    }
}
