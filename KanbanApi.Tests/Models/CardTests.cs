using KanbanApi.Models; 
using FluentAssertions; 
using Xunit;            

public class CardTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldSetProperties()
    {
        var board = new Board("Test Board");
        var column = new Column("Backlog", 0, board);
        var card = new Card("Implement feature", column);

        card.Title.Should().Be("Implement feature");
        card.Column.Should().Be(column);
        card.Column.Cards.Should().Contain(card); // if constructor adds automatically
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTitle_ShouldThrowArgumentException(string invalidTitle)
    {
        var board = new Board("Test Board");
        var column = new Column("Backlog", 0, board);

        Action act = () => new Card(invalidTitle, column);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Card title cannot be empty!*")
            .WithParameterName("title");
    }

    [Fact]
    public void Constructor_WithNullColumn_ShouldThrowArgumentNullException()
    {
        Action act = () => new Card("Test Card", null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
