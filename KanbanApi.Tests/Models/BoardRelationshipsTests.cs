using KanbanApi.Data;
using KanbanApi.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KanbanApi.Tests.Data;

public class BoardRelationshipsTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Board_ShouldLoadMembersCorrectly()
    {
        using var context = CreateContext("BoardMembers");

        var board = new Board("Test Board");
        context.Boards.Add(board);
        context.SaveChanges();

        var member = new BoardMember("user1", board.Id);
        context.BoardMembers.Add(member);
        context.SaveChanges();

        var loadedBoard = context.Boards
            .Include(b => b.Members)
            .Single();

        loadedBoard.Members.Should().HaveCount(1);
    }

    [Fact]
    public void Board_ShouldLoadColumnsCorrectly()
    {
        using var context = CreateContext("BoardColumns");

        var board = new Board("Test Board");
        context.Boards.Add(board);
        context.SaveChanges();

        var loadedBoard = context.Boards
            .Include(b => b.Columns)
            .Single();

        loadedBoard.Columns.Should().HaveCount(4);
    }
}
