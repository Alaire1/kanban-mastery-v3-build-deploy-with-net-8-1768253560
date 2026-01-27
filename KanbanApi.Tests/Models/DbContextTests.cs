using KanbanApi.Data;
using KanbanApi.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KanbanApi.Tests.Data;

public class ApplicationDbContextTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public void DbContext_ShouldBeCreated()
    {
        using var context = CreateContext("CreateContext");

        context.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_ShouldExposeAllDbSets()
    {
        using var context = CreateContext("DbSets");

        context.Boards.Should().NotBeNull();
        context.Columns.Should().NotBeNull();
        context.Cards.Should().NotBeNull();
        context.BoardMembers.Should().NotBeNull();
    }

    [Fact]
    public void BoardMember_ShouldHaveCompositePrimaryKey()
    {
        using var context = CreateContext("CompositeKey");

        var entity = context.Model.FindEntityType(typeof(BoardMember));
        var key = entity!.FindPrimaryKey();

        key.Should().NotBeNull();
        key!.Properties.Select(p => p.Name)
            .Should().BeEquivalentTo(new[] { "UserId", "BoardId" });
    }

    [Fact]
    public void BoardMember_ShouldNotAllowDuplicateUserBoardPairs()
    {
        using var context = CreateContext("DuplicateMembers");

        var board = new Board("Test Board");
        context.Boards.Add(board);
        context.SaveChanges();

        var member1 = new BoardMember("user1", board.Id);
        var member2 = new BoardMember("user1", board.Id);

        context.BoardMembers.Add(member1);

        Action act = () => context.BoardMembers.Add(member2);

        act.Should().Throw<InvalidOperationException>()
        .WithMessage("*cannot be tracked*");
    }
}
