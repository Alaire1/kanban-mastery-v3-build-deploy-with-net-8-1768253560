using KanbanApi.Models; 
using FluentAssertions; 
using Xunit;

namespace KanbanApi.Tests.Models;

public class ColumnTests
{
    
    //BASIC TESTS
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ShouldThrowArgumentException(string invalidName)
    {
        var board = new Board("Test Board");
        
        Action act = () => new Column(invalidName, 0, board);
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("Column name cannot be empty!*")
            .WithParameterName("name");
    }
    
    //ADD CARD
    [Fact]
    public void AddCard_ShouldAddCardToColumn()
    {
    
        var board = new Board("Test Board");
        var column = board.Columns.Single(c => c.Name == "To Do");

        var card = column.AddCard("Fix bug");

        column.Cards.Should().ContainSingle(c => c.Title == "Fix bug");
        card.Column.Should().Be(column);
    }

    [Fact]
    public void AddCard_NullOrEmptyTitle_ShouldThrowArgumentException()
    {
    var board = new Board("Test Board");
    // only choose column called "To Do"
    var column = board.Columns.Single(c => c.Name == "To Do");

    
    Action actNull = () => column.AddCard(null!);
    Action actEmpty = () => column.AddCard("");
    Action actWhitespace = () => column.AddCard("   ");


    actNull.Should().Throw<ArgumentException>()
        .WithParameterName("title");

    actEmpty.Should().Throw<ArgumentException>()
        .WithParameterName("title");

    actWhitespace.Should().Throw<ArgumentException>()
        .WithParameterName("title");
    }

    [Fact]
    public void AddCard_AllowsMultipleCardsInSameColumn()
    {
        var board = new Board("Test Board");
        var column = board.Columns.Single(c => c.Name == "To Do");

        column.AddCard("Fix bug");
        column.AddCard("Write tests");
        column.AddCard("Refactor");

        column.Cards.Should().HaveCount(3);
    }

    [Fact]
    public void AddCard_DoesNotAddCardToOtherColumns()
    {
        var board = new Board("Test Board");
        var todo = board.Columns.Single(c => c.Name == "To Do");
        var done = board.Columns.Single(c => c.Name == "Done");

        todo.AddCard("Fix bug");

        todo.Cards.Should().HaveCount(1);
        done.Cards.Should().BeEmpty();
    }
}