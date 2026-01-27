using KanbanApi.Models;
using FluentAssertions;
using Xunit;

namespace KanbanApi.Tests.Models;

public class BoardTests
{
    [Fact]
    public void Constructor_WithValidName_ShouldCreateBoard()
    {
        var board = new Board("My Project");
        
        board.Name.Should().Be("My Project");
        board.Id.Should().Be(0);
        
        // Users should exist and be empty
        board.Users.Should().NotBeNull();
        board.Users.Should().BeEmpty();
        
        // Columns should exist and have 4 items
        board.Columns.Should().NotBeNull();
        board.Columns.Should().HaveCount(4);
    }
    
    [Fact]
    public void Constructor_WithValidName_ShouldCreateFourDefaultColumns()
    {
        var board = new Board("Test Board");
        
        board.Columns.Should().HaveCount(4);
        board.Columns[0].Name.Should().Be("Backlog");
        board.Columns[0].Position.Should().Be(0);
        board.Columns[1].Name.Should().Be("To Do");
        board.Columns[1].Position.Should().Be(1);
        board.Columns[2].Name.Should().Be("In Progress");
        board.Columns[2].Position.Should().Be(2);
        board.Columns[3].Name.Should().Be("Done");
        board.Columns[3].Position.Should().Be(3);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Constructor_WithInvalidName_ShouldThrowArgumentException(string invalidName)
    {
        Action act = () => new Board(invalidName);
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("Board name cannot be empty!*")
            .WithParameterName("name");
    }
    
    [Fact]
    public void AddUser_WithValidUser_ShouldAddUserToBoard()
    {
        var board = new Board("Test Board");
        var user = new BoardMember("user1", 1);
        
        board.AddUser(user);
        
        board.Users.Should().ContainSingle();
        board.Users.Should().Contain(user);
    }
    
    [Fact]
    public void AddUser_WithNullUser_ShouldThrowArgumentNullException()
    {
        var board = new Board("Test Board");
        
        Action act = () => board.AddUser(null!);
        
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("user");
    }
    
    [Fact]
    public void AddUser_WithDuplicateUserId_ShouldThrowInvalidOperationException()
    {
        var board = new Board("Test Board");
        var user1 = new BoardMember("user1", 1);
        board.AddUser(user1);
        
        var user2 = new BoardMember("user1", 1); // Same UserId
        Action act = () => board.AddUser(user2);
        
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("User already exists on this board.");
    }
    
    [Fact]
    public void AddUser_WithMultipleDifferentUsers_ShouldAddAllUsers()
    {
        var board = new Board("Test Board");
        var user1 = new BoardMember("user1", 1);
        var user2 = new BoardMember("user2", 1);
        var user3 = new BoardMember("user3", 1);
        
        board.AddUser(user1);
        board.AddUser(user2);
        board.AddUser(user3);
        
        board.Users.Should().HaveCount(3);
        board.Users.Should().Contain(new[] { user1, user2, user3 });
    }
    
    [Fact]
    public void Columns_ShouldHaveSequentialPositions()
    {
        var board = new Board("Test Board");
        
        board.Columns.Should().OnlyHaveUniqueItems(c => c.Position);
        board.Columns.Should().BeInAscendingOrder(c => c.Position);
    }

    [Fact]
    public void Constructor_ShouldTrimBoardName()
    {
        var board = new Board("  My Board  ");

        board.Name.Should().Be("My Board");
    }

    [Fact]
    public void BoardColumns_ShouldAcceptCardsCorrectly()
    {
        var board = new Board("Test Board");
        var todo = board.Columns[1]; // "To Do"
        var card1 = new Card("Bug fix", todo);
        var card2 = new Card("Feature", todo);

        todo.Cards.Should().HaveCount(2);
        card1.Column.Should().Be(todo);
        card2.Column.Board.Should().Be(board); 
    }

}