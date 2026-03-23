using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KanbanApi;
using KanbanApi.Data;
using KanbanApi.Dtos;
using KanbanApi.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KanbanApi.Tests
{
    public class BoardTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly WebApplicationFactory<Program> _factory;

        public BoardTests(WebApplicationFactory<Program> factory)
        {
            var dbName = $"TestDb_Boards_{Guid.NewGuid()}";

            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace real DbContext with in-memory for tests
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase(dbName);
                    });

                    // Ensure database is created
                    var sp = services.BuildServiceProvider();
                    using (var scope = sp.CreateScope())
                    {
                        var scopedServices = scope.ServiceProvider;
                        var db = scopedServices.GetRequiredService<ApplicationDbContext>();
                        db.Database.EnsureCreated();
                    }
                });
            });
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task CreateBoard_WithValidData_ReturnsCreatedBoard()
        {
            // Arrange: Register and log in a user
            const string email = "boarduser@example.com";
            const string password = "Password123!";
            var registerResponse = await _client.PostAsJsonAsync("/register", new { email, password });
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            var loginResponse = await _client.PostAsJsonAsync("/login", new { email, password });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(auth);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

            // Get the current user's ID
            var userProfileResponse = await _client.GetAsync("/api/users/me");
            Assert.Equal(HttpStatusCode.OK, userProfileResponse.StatusCode);
            var userProfile = await userProfileResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
            Assert.NotNull(userProfile);

            // Act: Create a board
            var createBoardResponse = await _client.PostAsJsonAsync("/api/boards", new { BoardName = "Test Board" });

            // Assert
            Assert.Equal(HttpStatusCode.Created, createBoardResponse.StatusCode);
            var board = await createBoardResponse.Content.ReadFromJsonAsync<BoardDto>();
            Assert.NotNull(board);
            Assert.Equal("Test Board", board.Name);
            Assert.Equal(userProfile.Id, board.OwnerId); // OwnerId should match the logged-in user's ID
            Assert.Equal("Owner", board.Role);
            Assert.Single(board.Members); // Owner should be in the members list
            Assert.Contains(userProfile.Id, board.Members); // Owner's ID should be in members
            
            // Verify both Board and BoardMember records are created in the database
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var createdBoard = await db.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == board.Id);
                Assert.NotNull(createdBoard);
                Assert.Equal("Test Board", createdBoard.Name);
                Assert.Equal(userProfile.Id, createdBoard.OwnerId);
                
                var boardMember = createdBoard.Members.FirstOrDefault();
                Assert.NotNull(boardMember);
                Assert.Equal(userProfile.Id, boardMember.UserId);
                Assert.Equal("Owner", boardMember.Role);
            }
            
            // Print board data for visibility in test output
            TestConsole.FileHeader();
            Console.WriteLine($"\nBoard Data:\nId: {TestConsole.Value(board.Id, ConsoleColor.Cyan)}\nName: {TestConsole.Value(board.Name, ConsoleColor.Cyan)}\nOwnerId: {TestConsole.Value(board.OwnerId, ConsoleColor.Cyan)}\nRole: {TestConsole.Value(board.Role, ConsoleColor.Cyan)}\nMembers Count: {TestConsole.Value(board.Members.Count, ConsoleColor.Cyan)}");
            Console.WriteLine($"User ID: {TestConsole.Value(userProfile.Id, ConsoleColor.Green)}\nOwner ID Match: {TestConsole.Value(userProfile.Id == board.OwnerId, ConsoleColor.Green)}");
            Console.WriteLine($"Members: {TestConsole.Value($"[{string.Join(", ", board.Members)}]", ConsoleColor.Yellow)}\n");
        }

        [Fact]
        public async Task CreateBoard_WithoutAuthorization_ReturnsUnauthorized()
        {
            // Act: Attempt to create a board without a token
            var createBoardResponse = await _client.PostAsJsonAsync("/api/boards", new { Name = "Unauthorized Board" });

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, createBoardResponse.StatusCode);
        }

        [Fact]
        public async Task CreateBoard_WithInvalidData_ReturnsBadRequest()
        {
            // Arrange: Log in a user
            const string email = "invalidboarduser@example.com";
            const string password = "Password123!";
            var registerResponse = await _client.PostAsJsonAsync("/register", new { email, password });
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            var loginResponse = await _client.PostAsJsonAsync("/login", new { email, password });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(auth);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

            // Act: Attempt to create a board with invalid data
            var createBoardResponse = await _client.PostAsJsonAsync("/api/boards", new { BoardName = "" });

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, createBoardResponse.StatusCode);
        }
    }
}