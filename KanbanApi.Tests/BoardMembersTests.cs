using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KanbanApi.Data;
using KanbanApi.Dtos;
using KanbanApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KanbanApi.Tests;

public class BoardMembersTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BoardMembersTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_BoardMembers"));

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    /// Helper: register, login, return (client, userId, token).
    private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedUser(string email, string password = "Password123!")
    {
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/register", new { email, password });
        var loginResp = await client.PostAsJsonAsync("/login", new { email, password });
        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var profileResp = await client.GetAsync("/api/users/me");
        var profile = await profileResp.Content.ReadFromJsonAsync<UserProfileResponse>();

        return (client, profile!.Id);
    }

    /// Helper: create a board and return its ID.
    private async Task<int> CreateBoard(string ownerId, string boardName = "Test Board")
    {
        using var scope = _factory.Services.CreateScope();
        var boardService = scope.ServiceProvider.GetRequiredService<IBoardService>();
        var board = await boardService.CreateBoardAsync(boardName, ownerId);
        return board.Id;
    }

    // ──────────────────────────────────────────────
    //  Happy path: Owner adds a member
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AddMember_AsOwner_ReturnsCreated()
    {
        // Arrange – owner creates a board
        var (ownerClient, ownerId) = await CreateAuthenticatedUser("owner-add@example.com");
        var boardId = await CreateBoard(ownerId);

        // Register a second user to be added as member
        var (_, memberId) = await CreateAuthenticatedUser("newmember@example.com");

        // Act – owner adds the second user
        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new { UserId = memberId });

        Console.WriteLine(
            $"\nAddMember_AsOwner_ReturnsCreated -> HTTP {TestConsole.Value((int)response.StatusCode, ConsoleColor.Green)} ({TestConsole.Value(response.StatusCode, ConsoleColor.Green)})\n");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MemberResponse>();
        Assert.NotNull(body);
        Assert.Equal(boardId, body!.BoardId);
        Assert.Equal(memberId, body.UserId);
        Assert.Equal("Member", body.Role);

        // Verify in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var record = await db.BoardMembers
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == memberId);
        Assert.NotNull(record);
        Assert.Equal("Member", record!.Role);
    }

    // ──────────────────────────────────────────────
    //  Security: Non-owner gets 403
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AddMember_AsNonOwner_ReturnsForbidden()
    {
        // Arrange – owner creates a board
        var (_, ownerId) = await CreateAuthenticatedUser("owner123@example.com");
        var boardId = await CreateBoard(ownerId);

        // A second user who is NOT the owner
        var (nonOwnerClient, _) = await CreateAuthenticatedUser("nonOwner@example.com");

        // Register a third user to try adding
        var (_, thirdUserId) = await CreateAuthenticatedUser("added@example.com");

        // Act – non-owner tries to add a member
        var response = await nonOwnerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new { UserId = thirdUserId });

        Console.WriteLine(
            $"\nAddMember_AsNonOwner_ReturnsForbidden -> HTTP {TestConsole.Value((int)response.StatusCode, ConsoleColor.Red)} ({TestConsole.Value(response.StatusCode, ConsoleColor.Red)})\n");

        // Assert – must be 403, not 200/201
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify the member was NOT added in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.BoardMembers
            .AnyAsync(m => m.BoardId == boardId && m.UserId == thirdUserId);
        Assert.False(exists, "Non-owner should not be able to add members.");
    }

    // ──────────────────────────────────────────────
    //  Security: Unauthenticated request gets 401
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AddMember_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient(); // no token
        var response = await client.PostAsJsonAsync(
            "/api/boards/1/members",
            new { UserId = "anyone" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_MissingBoard_ReturnsNotFound()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner-missing-board@example.com");
        var (_, memberId) = await CreateAuthenticatedUser("missing-board-member@example.com");

        const int missingBoardId = 999_999;
        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{missingBoardId}/members",
            new { UserId = memberId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    //  Edge case: duplicate member returns 409
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AddMember_Duplicate_ReturnsConflict()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser("owner-dup@example.com");
        var boardId = await CreateBoard(ownerId);
        var (_, memberId) = await CreateAuthenticatedUser("dupmember@example.com");

        // First add – should succeed
        var first = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new { UserId = memberId });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Second add – should conflict
        var second = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new { UserId = memberId });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ──────────────────────────────────────────────
    //  Security: client-supplied role is ignored
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AddMember_WithOwnerRole_StillAssignsMemberRole()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser("owner-escalation@example.com");
        var boardId = await CreateBoard(ownerId);
        var (_, memberId) = await CreateAuthenticatedUser("escalator@example.com");

        Assert.NotEqual(ownerId, memberId);

        // Attacker tries to escalate by sending Role = "Owner"
        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new { UserId = memberId, Role = "Owner" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MemberResponse>();
        Assert.Equal("Member", body!.Role); // must be "Member", not "Owner"

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var memberIds = await db.BoardMembers
            .Where(m => m.BoardId == boardId)
            .Select(m => m.UserId)
            .ToListAsync();

        Console.WriteLine($"OwnerId: {TestConsole.Value(ownerId, ConsoleColor.Green)}");
        Console.WriteLine($"AddedMemberId: {TestConsole.Value(memberId, ConsoleColor.Yellow)}");
        Console.WriteLine($"Board {boardId} MemberIds: {TestConsole.Value($"[{string.Join(", ", memberIds)}]", ConsoleColor.Cyan)}");
    }

    // DTO for deserializing responses
    private record MemberResponse(int BoardId, string UserId, string Role);
}
