using KanbanApi.Models; 
using FluentAssertions; 
using Xunit;

namespace KanbanApi.Tests.Models;

public class ColumnTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreateColumn()
    {
        var board = new Board("Test Board");
        var column = new Column("Backlog", 0, board);
        
        column.Name.Should().Be("Backlog");
        column.Position.Should().Be(0);
        column.Board.Should().Be(board);
        column.Cards.Should().NotBeNull();
        column.Cards.Should().BeEmpty();
    }
    
    [Fact]
    public void Constructor_WithNullBoard_ShouldThrowArgumentNullException()
    {
        Action act = () => new Column("Backlog", 0, null!);
        
        act.Should().Throw<ArgumentNullException>();
    }
    
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
    
    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(100)]
    public void Constructor_WithInvalidPosition_ShouldThrowArgumentOutOfRangeException(int invalidPosition)
    {
        var board = new Board("Test Board");
        
        Action act = () => new Column("Backlog", invalidPosition, board);
        
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Position must be between 0 and 3*");
    }
    
    [Fact]
    public void AddCard_ShouldAddCardToColumn()
    {
        var board = new Board("Test Board");
        var column = board.Columns.ElementAt(1); // To Do
        var card = new Card("Fix bug", column);
        
        column.Cards.Should().ContainSingle(c => c.Title == "Fix bug");
        column.Cards.ElementAt(0).Column.Should().Be(column);
    }
    
    [Fact]
    public void AddCard_NullCard_ShouldThrowArgumentNullException()
    {
        var board = new Board("Test Board");
        var column = new Column("To Do", 1, board);
        
        Action act = () => column.AddCard(null!);
        
        act.Should().Throw<ArgumentNullException>();
    }
}