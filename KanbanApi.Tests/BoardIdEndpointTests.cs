using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KanbanApi;
using KanbanApi.Data;
using KanbanApi.Dtos;
using KanbanApi.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KanbanApi.Tests;

public class BoardIdEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public BoardIdEndpointTests(WebApplicationFactory<Program> factory)
    {
        var dbName = $"TestDb_BoardId_{Guid.NewGuid()}";
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

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

        var registerResponse = await client.PostAsJsonAsync("/register", new { email, password });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var profileResponse = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        var profile = await profileResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        return (client, profile!.Id);
    }

    private async Task<int> CreateBoard(HttpClient client, string boardName)
    {
        var response = await client.PostAsJsonAsync("/api/boards", new { BoardName = boardName });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<BoardDto>();
        Assert.NotNull(board);
        return board!.Id;
    }

    private async Task AddBoardMember(int boardId, string userId, string role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.BoardMembers.AnyAsync(m => m.BoardId == boardId && m.UserId == userId);
        if (!exists)
        {
            db.BoardMembers.Add(new BoardMember(userId, boardId, role));
            await db.SaveChangesAsync();
        }
    }

    private async Task AddCardToBoardColumn(int boardId, int columnPosition, string title, string? assignedUserId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var column = await db.Columns
            .Include(c => c.Cards)
            .FirstOrDefaultAsync(c => c.BoardId == boardId && c.Position == columnPosition);
        Assert.NotNull(column);
        var card = column!.AddCard(title);
        if (!string.IsNullOrWhiteSpace(assignedUserId))
            card.AssignedUserId = assignedUserId;
        await db.SaveChangesAsync();
    }

    private static async Task<BoardByIdResponse?> ReadBoardByIdResponse(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<BoardByIdResponse>(JsonOptions);

    private static void LogHttp(string testName, HttpStatusCode actual, HttpStatusCode expected)
    {
        var code = (int)actual;
        var isOk = actual == expected;
        var color = !isOk ? ConsoleColor.Red : code switch
        {
            >= 200 and < 300 => ConsoleColor.Green,
            >= 300 and < 400 => ConsoleColor.Yellow,
            _ => ConsoleColor.Red
        };
        TestConsole.FileHeader();
        Console.WriteLine();
        Console.WriteLine(TestConsole.Value(testName, ConsoleColor.Yellow));
        Console.WriteLine($"Result: {TestConsole.Value(isOk ? "OK" : "ERROR", isOk ? ConsoleColor.Green : ConsoleColor.Red)}");
        Console.WriteLine($"Status: {TestConsole.Value(code, color)} ({TestConsole.Value(actual, color)})");
        Console.WriteLine($"Message: {TestConsole.Value(isOk ? "No error" : $"Expected {expected} but got {actual}.", isOk ? ConsoleColor.Green : ConsoleColor.Red)}");
        Console.WriteLine();
    }

    private static void PrintBoardByIdDto(BoardByIdResponse dto)
    {
        TestConsole.FileHeader();
        var memberList = dto.Members.Count == 0
            ? "[]"
            : $"[{string.Join(", ", dto.Members.Select(m => $"{m.UserId}:{m.Role}"))}]";
        var columnList = dto.Columns.Count == 0
            ? "[]"
            : $"[{string.Join(", ", dto.Columns.Select(c => $"{c.Name}(pos:{c.Position}, cards:{c.Cards.Count})"))}]";
        Console.WriteLine(TestConsole.Value("BoardById DTO", ConsoleColor.Yellow));
        Console.WriteLine(
            $"Id: {TestConsole.Value(dto.Id, ConsoleColor.Cyan)}" +
            $"\nName: {TestConsole.Value(dto.Name, ConsoleColor.Cyan)}" +
            $"\nOwnerId: {TestConsole.Value(dto.OwnerId, ConsoleColor.Cyan)}" +
            $"\nRole: {TestConsole.Value(dto.Role, ConsoleColor.Cyan)}" +
            $"\nMembers: {TestConsole.Value(memberList, ConsoleColor.Yellow)}" +
            $"\nColumns: {TestConsole.Value(columnList, ConsoleColor.Green)}\n");
        Console.WriteLine();
    }

    // --- Private types ---

    private sealed record BoardByIdResponse(
        int Id, string Name, string OwnerId, string Role,
        List<MemberResponse> Members, List<ColumnResponse> Columns);
    private sealed record MemberResponse(string UserId, string Role);
    private sealed record ColumnResponse(int Id, string Name, int Position, List<CardResponse> Cards);
    private sealed record CardResponse(
        int Id,
        string Title,
        string? AssignedUserId,
        string? AssigneeUserName,
        string? AssigneeDisplayName);

    // --- GET /api/boards/{id} ---

    [Fact]
    public async Task GetBoardById_AsOwner_ReturnsOkWithOwnerRoleAndNestedData()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Owner Board");
        await AddCardToBoardColumn(boardId, 0, "Seed card");

        var response = await ownerClient.GetAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(GetBoardById_AsOwner_ReturnsOkWithOwnerRoleAndNestedData), response.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadBoardByIdResponse(response);
        Assert.NotNull(payload);
        Assert.Equal(boardId, payload!.Id);
        Assert.Equal(ownerId, payload.OwnerId);
        Assert.Equal("Owner", payload.Role);
        Assert.NotEmpty(payload.Members);
        Assert.Contains(payload.Members, m => m.UserId == ownerId && m.Role == "Owner");
        Assert.NotEmpty(payload.Columns);
        Assert.Contains(payload.Columns.SelectMany(c => c.Cards), c => c.Title == "Seed card");

        PrintBoardByIdDto(payload);
    }

    [Fact]
    public async Task GetBoardById_AsMember_ReturnsOkWithMemberRole()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (memberClient, memberId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Member Board");
        await AddBoardMember(boardId, memberId, "Member");

        var response = await memberClient.GetAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(GetBoardById_AsMember_ReturnsOkWithMemberRole), response.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadBoardByIdResponse(response);
        Assert.NotNull(payload);
        Assert.Equal(boardId, payload!.Id);
        Assert.Equal("Member", payload.Role);
        Assert.Contains(payload.Members, m => m.UserId == memberId && m.Role == "Member");

        PrintBoardByIdDto(payload);
    }

    [Fact]
    public async Task GetBoardById_AsNonMember_ReturnsForbidden()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (nonMemberClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Private Board");

        var response = await nonMemberClient.GetAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(GetBoardById_AsNonMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardById_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/boards/999/");
        LogHttp(nameof(GetBoardById_WithoutAuth_ReturnsUnauthorized), response.StatusCode, HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardById_NonExistentBoard_ReturnsForbidden()
    {
        // Returns 403 rather than 404 intentionally — the API does not
        // reveal whether a board exists to non-members.
        var (client, _) = await CreateAuthenticatedUser();
        var response = await client.GetAsync("/api/boards/999999/");
        LogHttp(nameof(GetBoardById_NonExistentBoard_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardById_DefaultColumns_AreOrderedAndComplete()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Ordering Board");

        var response = await ownerClient.GetAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(GetBoardById_DefaultColumns_AreOrderedAndComplete), response.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadBoardByIdResponse(response);
        Assert.NotNull(payload);
        PrintBoardByIdDto(payload!);

        var columns = payload!.Columns;
        Assert.Equal(4, columns.Count);
        Assert.Equal(columns.Select(c => c.Position).OrderBy(p => p).ToList(),
                     columns.Select(c => c.Position).ToList());
        Assert.Equal(new[] { "Backlog", "To Do", "In Progress", "Done" },
                     columns.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task GetBoardById_AssignedCard_IncludesAssigneeProfileFields()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (_, memberId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Assigned Card Board");
        await AddBoardMember(boardId, memberId, "Member");
        await AddCardToBoardColumn(boardId, 0, "Assigned card", memberId);

        var response = await ownerClient.GetAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(GetBoardById_AssignedCard_IncludesAssigneeProfileFields), response.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadBoardByIdResponse(response);
        Assert.NotNull(payload);

        var card = payload!.Columns.SelectMany(c => c.Cards).FirstOrDefault(c => c.Title == "Assigned card");
        Assert.NotNull(card);
        Assert.Equal(memberId, card!.AssignedUserId);
        Assert.False(string.IsNullOrWhiteSpace(card.AssigneeUserName));
    }

    // --- PUT /api/boards/{id} ---

    [Fact]
    public async Task UpdateBoard_AsOwner_ReturnsOkWithUpdatedName()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Original Name");

        var response = await ownerClient.PutAsJsonAsync($"/api/boards/{boardId}/", new { Name = "Updated Name" });
        LogHttp(nameof(UpdateBoard_AsOwner_ReturnsOkWithUpdatedName), response.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadBoardByIdResponse(response);
        Assert.NotNull(payload);
        Assert.Equal("Updated Name", payload!.Name);
        Assert.Equal(ownerId, payload.OwnerId);
        Assert.Equal("Owner", payload.Role);

        PrintBoardByIdDto(payload);
    }

    [Fact]
    public async Task UpdateBoard_AsMember_ReturnsForbidden()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (memberClient, memberId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Member Cannot Update");
        await AddBoardMember(boardId, memberId, "Member");

        var response = await memberClient.PutAsJsonAsync($"/api/boards/{boardId}/", new { Name = "Hacked Name" });
        LogHttp(nameof(UpdateBoard_AsMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_AsNonMember_ReturnsForbidden()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (nonMemberClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Non Member Cannot Update");

        var response = await nonMemberClient.PutAsJsonAsync($"/api/boards/{boardId}/", new { Name = "Hacked Name" });
        LogHttp(nameof(UpdateBoard_AsNonMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/boards/999/", new { Name = "No Auth" });
        LogHttp(nameof(UpdateBoard_WithoutAuth_ReturnsUnauthorized), response.StatusCode, HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_NonExistentBoard_ReturnsForbidden()
    {
        // Returns 403 rather than 404 intentionally — the API does not
        // reveal whether a board exists to non-members.
        var (client, _) = await CreateAuthenticatedUser();
        var response = await client.PutAsJsonAsync("/api/boards/999999/", new { Name = "Ghost Board" });
        LogHttp(nameof(UpdateBoard_NonExistentBoard_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_InvalidName_ReturnsBadRequest()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Valid Name");

        var response = await ownerClient.PutAsJsonAsync($"/api/boards/{boardId}/", new { Name = "!!! invalid ###" });
        LogHttp(nameof(UpdateBoard_InvalidName_ReturnsBadRequest), response.StatusCode, HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- DELETE /api/boards/{id} ---

    [Fact]
    public async Task DeleteBoard_AsOwner_ReturnsNoContent()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Board To Delete");

        var response = await ownerClient.DeleteAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(DeleteBoard_AsOwner_ReturnsNoContent), response.StatusCode, HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_AsOwner_BoardNoLongerAccessible()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Board Gone After Delete");

        await ownerClient.DeleteAsync($"/api/boards/{boardId}/");

        var response = await ownerClient.GetAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(DeleteBoard_AsOwner_BoardNoLongerAccessible), response.StatusCode, HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_AsMember_ReturnsForbidden()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (memberClient, memberId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Member Cannot Delete");
        await AddBoardMember(boardId, memberId, "Member");

        var response = await memberClient.DeleteAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(DeleteBoard_AsMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_AsNonMember_ReturnsForbidden()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (nonMemberClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Non Member Cannot Delete");

        var response = await nonMemberClient.DeleteAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(DeleteBoard_AsNonMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.DeleteAsync("/api/boards/999/");
        LogHttp(nameof(DeleteBoard_WithoutAuth_ReturnsUnauthorized), response.StatusCode, HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_NonExistentBoard_ReturnsForbidden()
    {
        // Returns 403 rather than 404 intentionally — the API does not
        // reveal whether a board exists to non-members.
        var (client, _) = await CreateAuthenticatedUser();
        var response = await client.DeleteAsync("/api/boards/999999/");
        LogHttp(nameof(DeleteBoard_NonExistentBoard_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_WithColumnsAndCards_RemovesAllRelatedData()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Cascade Delete Board");
        await AddCardToBoardColumn(boardId, 0, "Card to be deleted");

        // Capture counts before deletion for logging
        int columnCountBefore, cardCountBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            columnCountBefore = await db.Columns.CountAsync(c => c.BoardId == boardId);
            cardCountBefore = await db.Cards.CountAsync(card => card.Column.BoardId == boardId);
        }

        var response = await ownerClient.DeleteAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(DeleteBoard_WithColumnsAndCards_RemovesAllRelatedData), response.StatusCode, HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Null(await verifyDb.Boards.FindAsync(boardId));
        Assert.Empty(await verifyDb.Columns.Where(c => c.BoardId == boardId).ToListAsync());
        Assert.Empty(await verifyDb.Cards.Where(c => c.Column.BoardId == boardId).ToListAsync());

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\nBoardId: {TestConsole.Value(boardId, ConsoleColor.Cyan)}" +
            $"\nColumns before: {TestConsole.Value(columnCountBefore, ConsoleColor.Yellow)}" +
            $"\nCards before: {TestConsole.Value(cardCountBefore, ConsoleColor.Yellow)}" +
            $"\nAll cascade-deleted: {TestConsole.Value(true, ConsoleColor.Green)}\n");
    }
}