#nullable disable
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KanbanApi.Data;
using KanbanApi.Models;
using KanbanApi.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KanbanApi.Tests.Services
{
    public class BoardServiceTests
    {
        private const string DefaultOwnerId = "owner123";

        private static (ApplicationDbContext context, BoardService service) CreateContextAndService()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new ApplicationDbContext(options);
            var service = new BoardService(context);
            return (context, service);
        }
        // Helper method to seed an owner user into the context for testing
        private static void SeedOwner(ApplicationDbContext context, string ownerId = DefaultOwnerId)
        {
            if (context.Users.Any(u => u.Id == ownerId))
                return;

            var owner = new ApplicationUser("owner" + ownerId)
            {
                Id = ownerId,
                Email = ownerId + "@test.com"
            };

            context.Users.Add(owner);
            context.SaveChanges();
        }

        [Fact]
        public async Task CreateBoardAsync_ShouldCreateBoard_WhenNameIsValid()
        {
            var (context, service) = CreateContextAndService();
            var boardName = "Test Board";
            var ownerId = DefaultOwnerId;
            SeedOwner(context, ownerId);
            var result = await service.CreateBoardAsync(boardName, ownerId);

            
            Assert.NotNull(result);
            Assert.Equal(boardName, result.Name); 
            Assert.Equal("owner123", result.OwnerId);
            Assert.Single(context.Boards); //only one board should be created
        }

        [Fact]
        public async Task CreateBoardAsync_ShouldThrowArgumentException_WhenNameIsNull()
        {
            var (context, service) = CreateContextAndService();
            string boardName = null;
            var ownerId = DefaultOwnerId;
            SeedOwner(context, ownerId);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateBoardAsync(boardName, ownerId));
        }

        [Fact]
        public async Task GetBoardByIdAsync_ShouldReturnBoard_WhenBoardExists()
        {
            var (context, service) = CreateContextAndService();
            SeedOwner(context);
            var board = new Board("Test Board", DefaultOwnerId);
            context.Boards.Add(board);
            await context.SaveChangesAsync();

            var result = await service.GetBoardByIdAsync(board.Id);

            Assert.NotNull(result);
            Assert.Equal(board.Id, result.Id);
            Assert.Equal(board.OwnerId, result.OwnerId);
        }

        [Fact]
        public async Task GetBoardByIdAsync_ShouldReturnNull_WhenBoardDoesNotExist()
        {
            var (context, service) = CreateContextAndService();

            var result = await service.GetBoardByIdAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllBoardsAsync_ShouldReturnAllBoards()
        {
            var (context, service) = CreateContextAndService();
            var ownerId = DefaultOwnerId;
            SeedOwner(context, ownerId);
            context.Boards.Add(new Board("Board 1", ownerId));
            context.Boards.Add(new Board("Board 2", ownerId));
            await context.SaveChangesAsync();

            var result = await service.GetAllBoardsAsync();

            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task UpdateBoardAsync_ShouldPersistChanges()
        {
            var (context, service) = CreateContextAndService();
            var ownerId = DefaultOwnerId;
            SeedOwner(context, ownerId);
            var board = new Board("Original Name", ownerId);
            context.Boards.Add(board);
            await context.SaveChangesAsync();

            // Use reflection to change the Name since it has a private setter
            var nameProperty = typeof(Board).GetProperty("Name", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            nameProperty!.SetValue(board, "Updated Name");

            await service.UpdateBoardAsync(board);

            var updated = await context.Boards.FindAsync(board.Id);
            Assert.NotNull(updated);
            Assert.Equal("Updated Name", updated!.Name);
        }

        [Fact]
        public async Task DeleteBoardAsync_ShouldRemoveBoard_WhenBoardExists()
        {
            var (context, service) = CreateContextAndService();
            var ownerId = DefaultOwnerId;
            SeedOwner(context, ownerId);
            var board = new Board("To Delete", ownerId);
            context.Boards.Add(board);
            await context.SaveChangesAsync();

            await service.DeleteBoardAsync(board.Id);

            Assert.Empty(context.Boards);
        }

        [Fact]
        public async Task DeleteBoardAsync_ShouldDoNothing_WhenBoardDoesNotExist()
        {
            var (context, service) = CreateContextAndService();
            var ownerId = DefaultOwnerId;
            SeedOwner(context, ownerId);
            var board = new Board("Existing", ownerId);
            context.Boards.Add(board);
            await context.SaveChangesAsync();

            var countBefore = context.Boards.Count();

            await service.DeleteBoardAsync(board.Id + 1);

            var countAfter = context.Boards.Count();
            Assert.Equal(countBefore, countAfter);
        }

        [Fact]
        public async Task GetBoardsByUserIdAsync_ShouldReturnBoardsForUser()
        {
            var (context, service) = CreateContextAndService();

            var ownerId = DefaultOwnerId;
            SeedOwner(context, ownerId);
            var board1 = new Board("Board 1", ownerId);
            var board2 = new Board("Board 2", ownerId);
            context.Boards.AddRange(board1, board2);
            await context.SaveChangesAsync();
            
            var member1 = new BoardMember("1", board1.Id);
            var member2 = new BoardMember("2", board2.Id);
            context.BoardMembers.AddRange(member1, member2);
            await context.SaveChangesAsync();

            var result = await service.GetBoardsByUserIdAsync(1);

            Assert.Single(result);
            Assert.Equal("Board 1", result.First().Name);
        }
    }
}