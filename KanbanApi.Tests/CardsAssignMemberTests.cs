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

public class CardsAssignEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CardsAssignEndpointTests(WebApplicationFactory<Program> factory)
    {
        var dbName = $"TestDb_CardsAssign_{Guid.NewGuid()}";
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

    // --- Auth / board / card helpers ---

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

    private async Task AddBoardMember(int boardId, string userId, string role = "Member")
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

    /// <summary>
    /// Creates a column and a card inside it directly via the DB,
    /// bypassing the HTTP layer so tests stay focused on the assign endpoint.
    /// </summary>
    private async Task<int> CreateCardInBoard(int boardId, string cardTitle = "Test Card")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var column = await db.Columns.FirstOrDefaultAsync(c => c.BoardId == boardId);
        Assert.NotNull(column);

        var card = column!.AddCard(cardTitle);
        db.Cards.Add(card);
        await db.SaveChangesAsync();
        return card.Id;
    }

    // --- Logging helpers ---

    private static void LogHttp(string operation, HttpStatusCode actual, HttpStatusCode expected)
    {
        var statusCode = (int)actual;
        var color = actual != expected ? ConsoleColor.Red : statusCode switch
        {
            >= 200 and < 300 => ConsoleColor.Green,
            >= 300 and < 400 => ConsoleColor.Yellow,
            _ => ConsoleColor.Red
        };
        TestConsole.FileHeader();
        Console.WriteLine($"\n{operation} -> HTTP {TestConsole.Value((int)actual, color)} ({TestConsole.Value(actual, color)})");
    }

    private static void PrintAssignDto(string label, AssignCardResponseDto dto)
    {
        TestConsole.FileHeader();
        Console.WriteLine(
            $"\n{label}:" +
            $"\nCardId: {TestConsole.Value(dto.CardId, ConsoleColor.Cyan)}" +
            $"\nAssignedUserId: {TestConsole.Value(dto.UserId, ConsoleColor.Cyan)}\n");
    }

    private static void PrintErrorResponse(string label, string errorBody)
    {
        TestConsole.FileHeader();
        Console.WriteLine($"\n{label}: {TestConsole.Value(TrimForConsole(errorBody), ConsoleColor.Yellow)}\n");
    }

    private static string TrimForConsole(string text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text)) return "<empty>";
        var singleLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return singleLine.Length <= maxLength ? singleLine : $"{singleLine[..maxLength]}...";
    }

    // --- Tests ---

    [Fact]
    public async Task AssignCard_AsMember_ToAnotherMember_ReturnsOk()
    {
        // Assigning member assigns a card to a fellow board member — the happy path.
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (assignerClient, assignerId) = await CreateAuthenticatedUser();
        var (_, assigneeId) = await CreateAuthenticatedUser();

        var boardId = await CreateBoard(ownerClient, "Assign Happy Path Board");
        await AddBoardMember(boardId, assignerId);
        await AddBoardMember(boardId, assigneeId);

        var cardId = await CreateCardInBoard(boardId, "Card to assign");

        var response = await assignerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/cards/{cardId}/assign",
            new { UserId = assigneeId });

        LogHttp(nameof(AssignCard_AsMember_ToAnotherMember_ReturnsOk), response.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AssignCardResponseDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(cardId, dto!.CardId);
        Assert.Equal(assigneeId, dto.UserId);
        PrintAssignDto("Assigned card", dto);

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var card = await db.Cards.FindAsync(cardId);
        Assert.Equal(assigneeId, card!.AssignedUserId);

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\nBoardId: {TestConsole.Value(boardId, ConsoleColor.Cyan)}" +
            $"\nCardId: {TestConsole.Value(cardId, ConsoleColor.Cyan)}" +
            $"\nAssignerId: {TestConsole.Value(assignerId, ConsoleColor.Yellow)}" +
            $"\nAssigneeId: {TestConsole.Value(assigneeId, ConsoleColor.Green)}" +
            $"\nPersistedAssignedUserId: {TestConsole.Value(card.AssignedUserId, ConsoleColor.Green)}\n");
    }

    [Fact]
    public async Task AssignCard_AsOwner_ToMember_ReturnsOk()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var (_, memberId) = await CreateAuthenticatedUser();

        var boardId = await CreateBoard(ownerClient, "Owner Assign Board");
        await AddBoardMember(boardId, memberId);

        var cardId = await CreateCardInBoard(boardId, "Owner assigns this");

        var response = await ownerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/cards/{cardId}/assign",
            new { UserId = memberId });

        LogHttp(nameof(AssignCard_AsOwner_ToMember_ReturnsOk), response.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AssignCardResponseDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(memberId, dto!.UserId);
        PrintAssignDto("Owner assigned card", dto);
    }

    [Fact]
    public async Task AssignCard_ToSelf_ReturnsOk()
    {
        // A member should be able to assign a card to themselves.
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (memberClient, memberId) = await CreateAuthenticatedUser();

        var boardId = await CreateBoard(ownerClient, "Self Assign Board");
        await AddBoardMember(boardId, memberId);

        var cardId = await CreateCardInBoard(boardId, "Self assigned card");

        var response = await memberClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/cards/{cardId}/assign",
            new { UserId = memberId });

        LogHttp(nameof(AssignCard_ToSelf_ReturnsOk), response.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AssignCardResponseDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(memberId, dto!.UserId);
        PrintAssignDto("Self assigned card", dto);
    }

    [Fact]
    public async Task AssignCard_WithoutAuth_ReturnsUnauthorized()
    {
        // Unauthenticated request — authorization middleware fires before anything else.
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Unauth Assign Board");
        var cardId = await CreateCardInBoard(boardId, "Card");

        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/cards/{cardId}/assign",
            new { UserId = "anyone" });

        LogHttp(nameof(AssignCard_WithoutAuth_ReturnsUnauthorized), response.StatusCode, HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AssignCard_AsNonMember_ReturnsForbidden()
    {
        // Authenticated but not a board member — IsBoardMember policy rejects the caller.
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (nonMemberClient, _) = await CreateAuthenticatedUser();
        var (_, targetMemberId) = await CreateAuthenticatedUser();

        var boardId = await CreateBoard(ownerClient, "NonMember Assign Board");
        await AddBoardMember(boardId, targetMemberId);
        var cardId = await CreateCardInBoard(boardId, "Card");

        var response = await nonMemberClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/cards/{cardId}/assign",
            new { UserId = targetMemberId });

        LogHttp(nameof(AssignCard_AsNonMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignCard_ToNonMember_ReturnsBadRequest()
    {
        // The assigner is a valid member, but the target user is not on the board.
        // AssignCardAsync returns CardResult.Forbidden() in this case.
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (assignerClient, assignerId) = await CreateAuthenticatedUser();
        var (_, outsiderId) = await CreateAuthenticatedUser(); // never added to board

        var boardId = await CreateBoard(ownerClient, "Assign NonMember Target Board");
        await AddBoardMember(boardId, assignerId);

        var cardId = await CreateCardInBoard(boardId, "Card with outsider target");

        var response = await assignerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/cards/{cardId}/assign",
            new { UserId = outsiderId });

        LogHttp(nameof(AssignCard_ToNonMember_ReturnsBadRequest), response.StatusCode, HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\nAssignerId (board member): {TestConsole.Value(assignerId, ConsoleColor.Green)}" +
            $"\nOutsiderId (not on board): {TestConsole.Value(outsiderId, ConsoleColor.Red)}" +
            $"\nExpected: {TestConsole.Value("400 Bad Request", ConsoleColor.Yellow)}\n");
    }

    [Fact]
    public async Task AssignCard_NonExistentCard_ReturnsNotFound()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Missing Card Board");

        const int missingCardId = 999_999;

        var response = await ownerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/cards/{missingCardId}/assign",
            new { UserId = ownerId });

        LogHttp(nameof(AssignCard_NonExistentCard_ReturnsNotFound), response.StatusCode, HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var errorBody = await response.Content.ReadAsStringAsync();
        PrintErrorResponse("AssignCard missing card", errorBody);
    }

    [Fact]
    public async Task AssignCard_NonExistentBoard_ReturnsForbidden()
    {
        // The API does not reveal whether a board exists to non-members.
        // IsBoardMember policy fails before reaching service logic.
        var (client, _) = await CreateAuthenticatedUser();
        const int missingBoardId = 999_999;

        var response = await client.PutAsJsonAsync(
            $"/api/boards/{missingBoardId}/cards/1/assign",
            new { UserId = "anyone" });

        LogHttp(nameof(AssignCard_NonExistentBoard_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignCard_EmptyUserId_ReturnsBadRequest()
    {
        // Validation filter should reject an empty UserId before hitting the service.
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Validation Board");
        var cardId = await CreateCardInBoard(boardId, "Card");

        var response = await ownerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/cards/{cardId}/assign",
            new { UserId = "" });

        LogHttp(nameof(AssignCard_EmptyUserId_ReturnsBadRequest), response.StatusCode, HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorBody = await response.Content.ReadAsStringAsync();
        PrintErrorResponse("AssignCard empty UserId", errorBody);

        using var doc = JsonDocument.Parse(errorBody);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors), "Missing 'errors'");
        Assert.True(errors.TryGetProperty("UserId", out _), "Missing 'UserId' in errors");
    }

    [Fact]
    public async Task AssignCard_ReassignToDifferentMember_ReturnsOk()
    {
        // Assigning again to a different member should overwrite the previous assignment.
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (_, firstMemberId) = await CreateAuthenticatedUser();
        var (_, secondMemberId) = await CreateAuthenticatedUser();

        var boardId = await CreateBoard(ownerClient, "Reassign Board");
        await AddBoardMember(boardId, firstMemberId);
        await AddBoardMember(boardId, secondMemberId);

        var cardId = await CreateCardInBoard(boardId, "Card to reassign");

        // First assignment
        var firstResponse = await ownerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/cards/{cardId}/assign",
            new { UserId = firstMemberId });
        LogHttp(nameof(AssignCard_ReassignToDifferentMember_ReturnsOk) + " [first]", firstResponse.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Second assignment — overwrites the first
        var secondResponse = await ownerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/cards/{cardId}/assign",
            new { UserId = secondMemberId });
        LogHttp(nameof(AssignCard_ReassignToDifferentMember_ReturnsOk) + " [second]", secondResponse.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var dto = await secondResponse.Content.ReadFromJsonAsync<AssignCardResponseDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(secondMemberId, dto!.UserId);
        PrintAssignDto("Reassigned card", dto);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var card = await db.Cards.FindAsync(cardId);
        Assert.Equal(secondMemberId, card!.AssignedUserId);

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\nFirst assignee: {TestConsole.Value(firstMemberId, ConsoleColor.Yellow)}" +
            $"\nFinal assignee: {TestConsole.Value(secondMemberId, ConsoleColor.Green)}" +
            $"\nPersistedAssignedUserId: {TestConsole.Value(card.AssignedUserId, ConsoleColor.Green)}\n");
    }

}