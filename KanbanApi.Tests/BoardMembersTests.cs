using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KanbanApi.Data;
using KanbanApi.Dtos;
using KanbanApi.Models;
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
        var dbName = $"TestDb_BoardMembers_{Guid.NewGuid()}";

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

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

        TestConsole.FileHeader();

        Console.WriteLine(
            $"\nAddMember_AsOwner_ReturnsCreated -> HTTP {TestConsole.Value((int)response.StatusCode, ConsoleColor.Green)} ({TestConsole.Value(response.StatusCode, ConsoleColor.Green)})");

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

    [Fact]
    public async Task AddMember_AsOwner_AddsSeveralMembers()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser("owner-several@example.com");
        var boardId = await CreateBoard(ownerId, "Several Members Board");

        var (_, member1Id) = await CreateAuthenticatedUser("member1-several@example.com");
        var (_, member2Id) = await CreateAuthenticatedUser("member2-several@example.com");
        var (_, member3Id) = await CreateAuthenticatedUser("member3-several@example.com");

        var memberIds = new[] { member1Id, member2Id, member3Id };

        foreach (var memberId in memberIds)
        {
            var response = await ownerClient.PostAsJsonAsync(
                $"/api/boards/{boardId}/members",
                new { UserId = memberId });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<MemberResponse>();
            Assert.NotNull(body);
            Assert.Equal(boardId, body!.BoardId);
            Assert.Equal(memberId, body.UserId);
            Assert.Equal("Member", body.Role);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var boardMembers = await db.BoardMembers
            .Where(m => m.BoardId == boardId)
            .ToListAsync();

        Assert.Equal(memberIds.Length + 1, boardMembers.Count); // +1 for owner
        Assert.All(memberIds, id => Assert.Contains(boardMembers, m => m.UserId == id && m.Role == "Member"));
        Assert.Contains(boardMembers, m => m.UserId == ownerId && m.Role == "Owner");

        var addedMembersList = string.Join(", ", memberIds.Select(id => TestConsole.Value(id, ConsoleColor.Yellow)));

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\n=== AddMember_AsOwner_AddsSeveralMembers ===" +
            $"\nBoardId: {TestConsole.Value(boardId, ConsoleColor.Cyan)}" +
            $"\nOwnerId: {TestConsole.Value(ownerId, ConsoleColor.Green)}" +
            $"\nCreatedMember1Id: {TestConsole.Value(member1Id, ConsoleColor.Yellow)}" +
            $"\nCreatedMember2Id: {TestConsole.Value(member2Id, ConsoleColor.Yellow)}" +
            $"\nCreatedMember3Id: {TestConsole.Value(member3Id, ConsoleColor.Yellow)}" +
            $"\nAddedMemberIds: [{addedMembersList}]" +
            $"\nTotalMembersOnBoard: {TestConsole.Value(boardMembers.Count, ConsoleColor.Cyan)}\n");
    }

    [Fact]
    public async Task AddMember_MultiBoard_MultiOwner_CrossMemberships_Work()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser("owner-multi@example.com");
        var (member1Client, member1Id) = await CreateAuthenticatedUser("member1-multi@example.com");
        var (member2Client, member2Id) = await CreateAuthenticatedUser("member2-multi@example.com");
        var (_, member3Id) = await CreateAuthenticatedUser("member3-multi@example.com");

        var ownerBoard1 = await CreateBoard(ownerId, "Owner Board 1");
        var ownerBoard2 = await CreateBoard(ownerId, "Owner Board 2");
        var ownerBoard3 = await CreateBoard(ownerId, "Owner Board 3");

        async Task AddMemberAndAssertCreated(HttpClient client, int boardId, string userId)
        {
            var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/members", new { UserId = userId });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<MemberResponse>();
            Assert.NotNull(body);
            Assert.Equal(boardId, body!.BoardId);
            Assert.Equal(userId, body.UserId);
            Assert.Equal("Member", body.Role);
        }

        // Owner adds members across multiple owner-owned boards
        await AddMemberAndAssertCreated(ownerClient, ownerBoard1, member1Id);
        await AddMemberAndAssertCreated(ownerClient, ownerBoard1, member2Id);

        await AddMemberAndAssertCreated(ownerClient, ownerBoard2, member2Id);
        await AddMemberAndAssertCreated(ownerClient, ownerBoard2, member3Id);

        await AddMemberAndAssertCreated(ownerClient, ownerBoard3, member1Id);
        await AddMemberAndAssertCreated(ownerClient, ownerBoard3, member3Id);

        // Two additional boards owned by members
        var member1Board = await CreateBoard(member1Id, "Member1 Board");
        var member2Board = await CreateBoard(member2Id, "Member2 Board");

        // Member1 (as owner of member1Board) adds other people + original owner
        await AddMemberAndAssertCreated(member1Client, member1Board, member2Id);
        await AddMemberAndAssertCreated(member1Client, member1Board, member3Id);
        await AddMemberAndAssertCreated(member1Client, member1Board, ownerId);

        // Member2 (as owner of member2Board) adds other people + original owner
        await AddMemberAndAssertCreated(member2Client, member2Board, member1Id);
        await AddMemberAndAssertCreated(member2Client, member2Board, member3Id);
        await AddMemberAndAssertCreated(member2Client, member2Board, ownerId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var boardMemberships = await db.BoardMembers
            .Where(m => m.BoardId == ownerBoard1 || m.BoardId == ownerBoard2 || m.BoardId == ownerBoard3 || m.BoardId == member1Board || m.BoardId == member2Board)
            .ToListAsync();

        // Each board should have 3 members total (1 owner + 2 or 3 added depending on board)
        Assert.Equal(3, boardMemberships.Count(m => m.BoardId == ownerBoard1));
        Assert.Equal(3, boardMemberships.Count(m => m.BoardId == ownerBoard2));
        Assert.Equal(3, boardMemberships.Count(m => m.BoardId == ownerBoard3));
        Assert.Equal(4, boardMemberships.Count(m => m.BoardId == member1Board));
        Assert.Equal(4, boardMemberships.Count(m => m.BoardId == member2Board));

        Assert.Contains(boardMemberships, m => m.BoardId == ownerBoard1 && m.UserId == ownerId && m.Role == "Owner");
        Assert.Contains(boardMemberships, m => m.BoardId == member1Board && m.UserId == member1Id && m.Role == "Owner");
        Assert.Contains(boardMemberships, m => m.BoardId == member2Board && m.UserId == member2Id && m.Role == "Owner");

        var ownerBoardMemberIds = boardMemberships
            .Where(m => m.BoardId == ownerBoard1 || m.BoardId == ownerBoard2 || m.BoardId == ownerBoard3)
            .Select(m => $"B{m.BoardId}:{m.UserId}:{m.Role}")
            .ToList();

        var memberBoardMemberIds = boardMemberships
            .Where(m => m.BoardId == member1Board || m.BoardId == member2Board)
            .Select(m => $"B{m.BoardId}:{m.UserId}:{m.Role}")
            .ToList();

        var ownerBoardIds = new[] { ownerBoard1, ownerBoard2, ownerBoard3 };
        var memberOwnedBoardIds = new[] { member1Board, member2Board };
        var userColors = new Dictionary<string, ConsoleColor>
        {
            [ownerId] = ConsoleColor.Green,
            [member1Id] = ConsoleColor.Yellow,
            [member2Id] = ConsoleColor.Magenta,
            [member3Id] = ConsoleColor.Blue
        };

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\n=== AddMember_MultiBoard_MultiOwner_CrossMemberships_Work ===" +
            $"\nUsers" +
            $"\n  Owner:   {TestConsole.Value(ownerId, userColors[ownerId])}" +
            $"\n  Member1: {TestConsole.Value(member1Id, userColors[member1Id])}" +
            $"\n  Member2: {TestConsole.Value(member2Id, userColors[member2Id])}" +
            $"\n  Member3: {TestConsole.Value(member3Id, userColors[member3Id])}" +
            $"\nBoards" +
            $"\n  Owner-owned:  [{TestConsole.Value(string.Join(", ", ownerBoardIds), ConsoleColor.Cyan)}]" +
            $"\n  Member-owned: [{TestConsole.Value(string.Join(", ", memberOwnedBoardIds), ConsoleColor.Cyan)}]" +
            $"\nMemberships by board" +
            $"\n{FormatBoardMembershipBlock(ownerBoard1, boardMemberships, userColors)}" +
            $"\n{FormatBoardMembershipBlock(ownerBoard2, boardMemberships, userColors)}" +
            $"\n{FormatBoardMembershipBlock(ownerBoard3, boardMemberships, userColors)}" +
            $"\n{FormatBoardMembershipBlock(member1Board, boardMemberships, userColors)}" +
            $"\n{FormatBoardMembershipBlock(member2Board, boardMemberships, userColors)}" +
            $"\n");
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

        TestConsole.FileHeader();

        Console.WriteLine(
            $"\nAddMember_AsNonOwner_ReturnsForbidden -> HTTP {TestConsole.Value((int)response.StatusCode, ConsoleColor.Red)} ({TestConsole.Value(response.StatusCode, ConsoleColor.Red)})");

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

        TestConsole.FileHeader();

        Console.WriteLine($"OwnerId: {TestConsole.Value(ownerId, ConsoleColor.Green)}");
        Console.WriteLine($"AddedMemberId: {TestConsole.Value(memberId, ConsoleColor.Yellow)}");
        Console.WriteLine($"Board {boardId} MemberIds: {TestConsole.Value($"[{string.Join(", ", memberIds)}]", ConsoleColor.Cyan)}");
    }

    // DTO for deserializing responses
    private record MemberResponse(int BoardId, string UserId, string Role);

    private static string FormatBoardMembershipBlock(
        int boardId,
        IEnumerable<BoardMember> memberships,
        IReadOnlyDictionary<string, ConsoleColor> userColors)
    {
        var items = memberships
            .Where(m => m.BoardId == boardId)
            .OrderBy(m => m.Role == "Owner" ? 0 : 1)
            .ThenBy(m => m.UserId)
            .Select(m =>
            {
                var color = userColors.TryGetValue(m.UserId, out var resolved) ? resolved : ConsoleColor.White;
                var coloredUserId = TestConsole.Value(m.UserId, color);
                return $"    - {coloredUserId}:{m.Role}";
            });

        return $"  B{boardId} ->\n{string.Join("\n", items)}\n";
    }
}
