using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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

    // --- Helpers ---

    private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedUser(
        string? email = null, string password = "Password123!")
    {
        email ??= $"user_{Guid.NewGuid():N}@example.com";
        var client = _factory.CreateClient();

        var registerResp = await client.PostAsJsonAsync("/register", new { email, password });
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);

        var loginResp = await client.PostAsJsonAsync("/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var profileResp = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, profileResp.StatusCode);

        var profile = await profileResp.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        return (client, profile!.Id);
    }

    private async Task<int> CreateBoard(string ownerId, string boardName = "Test Board")
    {
        using var scope = _factory.Services.CreateScope();
        var boardService = scope.ServiceProvider.GetRequiredService<IBoardService>();
        var board = await boardService.CreateBoardAsync(boardName, ownerId);
        return board.Id;
    }

    private async Task<MemberResponse> AddMemberAndAssert(
        HttpClient client, int boardId, string userId,
        HttpStatusCode expected = HttpStatusCode.Created)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new { UserId = userId });
        Assert.Equal(expected, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(boardId.ToString(), body!.BoardId.ToString());
        Assert.Equal(userId, body.UserId);
        Assert.Equal("Member", body.Role);
        return body;
    }

    // --- Tests ---

    [Fact]
    public async Task AddMember_AsOwner_ReturnsCreated()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerId);
        var (_, memberId) = await CreateAuthenticatedUser();

        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new { UserId = memberId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(boardId.ToString(), body!.BoardId.ToString());
        Assert.Equal(memberId, body.UserId);
        Assert.Equal("Member", body.Role);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var record = await db.BoardMembers
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == memberId);
        Assert.NotNull(record);
        Assert.Equal("Member", record!.Role);

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\nAddMember_AsOwner_ReturnsCreated -> HTTP {TestConsole.Value((int)response.StatusCode, ConsoleColor.Green)} ({TestConsole.Value(response.StatusCode, ConsoleColor.Green)})");
    }

    [Fact]
    public async Task AddMember_AsOwner_AddsSeveralMembers()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerId, "Several Members Board");

        var (_, member1Id) = await CreateAuthenticatedUser();
        var (_, member2Id) = await CreateAuthenticatedUser();
        var (_, member3Id) = await CreateAuthenticatedUser();
        var memberIds = new[] { member1Id, member2Id, member3Id };

        foreach (var memberId in memberIds)
            await AddMemberAndAssert(ownerClient, boardId, memberId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var boardMembers = await db.BoardMembers.Where(m => m.BoardId == boardId).ToListAsync();

        Assert.Equal(memberIds.Length + 1, boardMembers.Count); // +1 for owner
        Assert.All(memberIds, id => Assert.Contains(boardMembers, m => m.UserId == id && m.Role == "Member"));
        Assert.Contains(boardMembers, m => m.UserId == ownerId && m.Role == "Owner");

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\nBoardId: {TestConsole.Value(boardId, ConsoleColor.Cyan)}" +
            $"\nOwnerId: {TestConsole.Value(ownerId, ConsoleColor.Green)}" +
            $"\nMember1Id: {TestConsole.Value(member1Id, ConsoleColor.Yellow)}" +
            $"\nMember2Id: {TestConsole.Value(member2Id, ConsoleColor.Yellow)}" +
            $"\nMember3Id: {TestConsole.Value(member3Id, ConsoleColor.Yellow)}" +
            $"\nTotalMembersOnBoard: {TestConsole.Value(boardMembers.Count, ConsoleColor.Cyan)}\n");
    }

    [Fact]
    public async Task AddMember_MultiBoard_CrossMemberships_Work()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var (member1Client, member1Id) = await CreateAuthenticatedUser();
        var (member2Client, member2Id) = await CreateAuthenticatedUser();
        var (_, member3Id) = await CreateAuthenticatedUser();

        var ownerBoard1 = await CreateBoard(ownerId, "Owner Board 1");
        var ownerBoard2 = await CreateBoard(ownerId, "Owner Board 2");
        var ownerBoard3 = await CreateBoard(ownerId, "Owner Board 3");

        // owner adds members across their boards
        await AddMemberAndAssert(ownerClient, ownerBoard1, member1Id);
        await AddMemberAndAssert(ownerClient, ownerBoard1, member2Id);
        await AddMemberAndAssert(ownerClient, ownerBoard2, member2Id);
        await AddMemberAndAssert(ownerClient, ownerBoard2, member3Id);
        await AddMemberAndAssert(ownerClient, ownerBoard3, member1Id);
        await AddMemberAndAssert(ownerClient, ownerBoard3, member3Id);

        // boards owned by members
        var member1Board = await CreateBoard(member1Id, "Member1 Board");
        var member2Board = await CreateBoard(member2Id, "Member2 Board");

        await AddMemberAndAssert(member1Client, member1Board, member2Id);
        await AddMemberAndAssert(member1Client, member1Board, member3Id);
        await AddMemberAndAssert(member1Client, member1Board, ownerId);
        await AddMemberAndAssert(member2Client, member2Board, member1Id);
        await AddMemberAndAssert(member2Client, member2Board, member3Id);
        await AddMemberAndAssert(member2Client, member2Board, ownerId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var allBoardIds = new[] { ownerBoard1, ownerBoard2, ownerBoard3, member1Board, member2Board };
        var memberships = await db.BoardMembers
            .Where(m => allBoardIds.Contains(m.BoardId))
            .ToListAsync();

        // owner boards: 1 owner + 2 added members each
        Assert.Equal(3, memberships.Count(m => m.BoardId == ownerBoard1));
        Assert.Equal(3, memberships.Count(m => m.BoardId == ownerBoard2));
        Assert.Equal(3, memberships.Count(m => m.BoardId == ownerBoard3));
        // member boards: 1 owner + 3 added members each
        Assert.Equal(4, memberships.Count(m => m.BoardId == member1Board));
        Assert.Equal(4, memberships.Count(m => m.BoardId == member2Board));

        Assert.Contains(memberships, m => m.BoardId == ownerBoard1 && m.UserId == ownerId && m.Role == "Owner");
        Assert.Contains(memberships, m => m.BoardId == member1Board && m.UserId == member1Id && m.Role == "Owner");
        Assert.Contains(memberships, m => m.BoardId == member2Board && m.UserId == member2Id && m.Role == "Owner");

        var userColors = new Dictionary<string, ConsoleColor>
        {
            [ownerId] = ConsoleColor.Green,
            [member1Id] = ConsoleColor.Yellow,
            [member2Id] = ConsoleColor.Magenta,
            [member3Id] = ConsoleColor.Blue
        };

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\nOwner:   {TestConsole.Value(ownerId, userColors[ownerId])}" +
            $"\nMember1: {TestConsole.Value(member1Id, userColors[member1Id])}" +
            $"\nMember2: {TestConsole.Value(member2Id, userColors[member2Id])}" +
            $"\nMember3: {TestConsole.Value(member3Id, userColors[member3Id])}\n" +
            string.Concat(allBoardIds.Select(id => FormatBoardMembershipBlock(id, memberships, userColors))));
    }

    [Fact]
    public async Task AddMember_AsNonOwner_ReturnsForbidden()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var (nonOwnerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerId);
        var (_, targetUserId) = await CreateAuthenticatedUser();

        var response = await nonOwnerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new { UserId = targetUserId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.BoardMembers.AnyAsync(m => m.BoardId == boardId && m.UserId == targetUserId);
        Assert.False(exists, "Non-owner should not be able to add members.");

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\nAddMember_AsNonOwner_ReturnsForbidden -> HTTP {TestConsole.Value((int)response.StatusCode, ConsoleColor.Red)} ({TestConsole.Value(response.StatusCode, ConsoleColor.Red)})");
    }

    [Fact]
    public async Task AddMember_AsBoardMember_ReturnsForbidden()
    {
        // A board member (not owner) should also not be able to add other members
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var (memberClient, memberId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerId);

        // make them a member first via the owner
        await AddMemberAndAssert(ownerClient, boardId, memberId);

        var (_, targetUserId) = await CreateAuthenticatedUser();
        var response = await memberClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new { UserId = targetUserId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.BoardMembers.AnyAsync(m => m.BoardId == boardId && m.UserId == targetUserId);
        Assert.False(exists, "Board member should not be able to add other members.");
    }

    [Fact]
    public async Task AddMember_WithoutAuth_ReturnsUnauthorized()
    {
        // Auth is checked before route resolution — board doesn't need to exist
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/boards/999/members",
            new { UserId = "anyone" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_NonExistentBoard_ReturnsForbidden()
    {
        // Returns 403 rather than 404 intentionally — the API does not
        // reveal whether a board exists to non-members.
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (_, memberId) = await CreateAuthenticatedUser();

        var response = await ownerClient.PostAsJsonAsync(
            "/api/boards/999999/members",
            new { UserId = memberId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_Duplicate_ReturnsConflict()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerId);
        var (_, memberId) = await CreateAuthenticatedUser();

        var first = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members", new { UserId = memberId });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members", new { UserId = memberId });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task AddMember_RoleFieldInBody_IsAlwaysIgnored()
    {
        // The role field in the request body must always be ignored —
        // every added member gets "Member" regardless of what is sent.
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerId);
        var (_, memberId) = await CreateAuthenticatedUser();

        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new { UserId = memberId, Role = "Owner" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MemberResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("Member", body!.Role);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var record = await db.BoardMembers.FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == memberId);
        Assert.NotNull(record);
        Assert.Equal("Member", record!.Role);
    }

    // --- Private types ---

    private record MemberResponse(string BoardId, string UserId, string Role);

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
                var color = userColors.TryGetValue(m.UserId, out var c) ? c : ConsoleColor.White;
                return $"    - {TestConsole.Value(m.UserId, color)}:{m.Role}";
            });
        return $"  B{boardId} ->\n{string.Join("\n", items)}\n";
    }
}