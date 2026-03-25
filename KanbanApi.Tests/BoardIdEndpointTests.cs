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
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    [Fact]
    public async Task GetBoardById_AsOwner_ReturnsOkWithOwnerRoleAndNestedData()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser("owner.by.id@example.com");
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

        PrintBoardByIdDto(payload);

        var allCards = payload.Columns.SelectMany(c => c.Cards).ToList();
        Assert.Contains(allCards, c => c.Title == "Seed card");
    }

    [Fact]
    public async Task GetBoardById_AsMember_ReturnsOkWithMemberRole()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.member.case@example.com");
        var (memberClient, memberId) = await CreateAuthenticatedUser("member.case@example.com");
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
    }

    [Fact]
    public async Task GetBoardById_AsNonMember_ReturnsForbidden()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.nonmember.case@example.com");
        var (nonMemberClient, _) = await CreateAuthenticatedUser("nonmember.case@example.com");
        var boardId = await CreateBoard(ownerClient, "Private Board");

        var response = await nonMemberClient.GetAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(GetBoardById_AsNonMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardById_WithoutAuth_ReturnsUnauthorized()
    {
        var unauthenticatedClient = _factory.CreateClient();
        var response = await unauthenticatedClient.GetAsync("/api/boards/123/");
        LogHttp(nameof(GetBoardById_WithoutAuth_ReturnsUnauthorized), response.StatusCode, HttpStatusCode.Unauthorized);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
        public async Task GetBoardById_MissingBoard_ReturnsForbidden()
    {
        var (client, _) = await CreateAuthenticatedUser("missing.board.case@example.com");

        var response = await client.GetAsync("/api/boards/999999/");

            LogHttp(nameof(GetBoardById_MissingBoard_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // No response body expected for Forbidden
    }

    [Fact]
    public async Task GetBoardById_DefaultColumns_AreOrderedAndComplete()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.order.case@example.com");
        var boardId = await CreateBoard(ownerClient, "Ordering Board");

        var response = await ownerClient.GetAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(GetBoardById_DefaultColumns_AreOrderedAndComplete), response.StatusCode, HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadBoardByIdResponse(response);
        Assert.NotNull(payload);

        PrintBoardByIdDto(payload!);

        var columns = payload!.Columns;
        Assert.Equal(4, columns.Count);

        var positions = columns.Select(c => c.Position).ToList();
        var sorted = positions.OrderBy(p => p).ToList();
        Assert.Equal(sorted, positions);

        var names = columns.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "Backlog", "To Do", "In Progress", "Done" }, names);
    }

    [Fact]
    public async Task GetBoardById_MemberRole_IsMember()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.custom.role@example.com");
        var (memberClient, memberId) = await CreateAuthenticatedUser("editor.custom.role@example.com");
        var boardId = await CreateBoard(ownerClient, "Custom Role Board");

        await AddBoardMember(boardId, memberId, "Member");

        var response = await memberClient.GetAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(GetBoardById_MemberRole_IsMember), response.StatusCode, HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadBoardByIdResponse(response);
        Assert.NotNull(payload);
        Assert.Equal("Member", payload!.Role);
        Assert.Contains(payload.Members, m => m.UserId == memberId && m.Role == "Member");

        PrintBoardByIdDto(payload);
    }

    [Fact]
    public async Task UpdateBoard_AsOwner_ReturnsOkWithUpdatedName()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser("owner.update@example.com");
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
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.update.member@example.com");
        var (memberClient, memberId) = await CreateAuthenticatedUser("member.update@example.com");
        var boardId = await CreateBoard(ownerClient, "Member Cannot Update");

        await AddBoardMember(boardId, memberId, "Member");

        var response = await memberClient.PutAsJsonAsync($"/api/boards/{boardId}/", new { Name = "Hacked Name" });
        LogHttp(nameof(UpdateBoard_AsMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

[Fact]
public async Task UpdateBoard_AsNonMember_ReturnsForbidden()
{
    var (ownerClient, _) = await CreateAuthenticatedUser("owner.update.nonmember@example.com");
    var (nonMemberClient, _) = await CreateAuthenticatedUser("nonmember.update@example.com");
    var boardId = await CreateBoard(ownerClient, "Non Member Cannot Update");

    var response = await nonMemberClient.PutAsJsonAsync($"/api/boards/{boardId}/", new { Name = "Hacked Name" });
    LogHttp(nameof(UpdateBoard_AsNonMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

[Fact]
public async Task UpdateBoard_WithoutAuth_ReturnsUnauthorized()
{
    var unauthenticatedClient = _factory.CreateClient();

    var response = await unauthenticatedClient.PutAsJsonAsync("/api/boards/123/", new { Name = "No Auth" });
    LogHttp(nameof(UpdateBoard_WithoutAuth_ReturnsUnauthorized), response.StatusCode, HttpStatusCode.Unauthorized);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
    public async Task UpdateBoard_MissingBoard_ReturnsForbidden()
{
    var (client, _) = await CreateAuthenticatedUser("owner.update.missing@example.com");

    var response = await client.PutAsJsonAsync("/api/boards/999999/", new { Name = "Ghost Board" });

        LogHttp(nameof(UpdateBoard_MissingBoard_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    // No response body expected for Forbidden
}

[Fact]
public async Task UpdateBoard_InvalidName_ReturnsBadRequest()
{
    var (ownerClient, _) = await CreateAuthenticatedUser("owner.update.invalid@example.com");
    var boardId = await CreateBoard(ownerClient, "Valid Name");

    var response = await ownerClient.PutAsJsonAsync($"/api/boards/{boardId}/", new { Name = "!!! invalid ###" });
    LogHttp(nameof(UpdateBoard_InvalidName_ReturnsBadRequest), response.StatusCode, HttpStatusCode.BadRequest);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}

[Fact]
public async Task DeleteBoard_AsOwner_ReturnsNoContent()
{
    var (ownerClient, _) = await CreateAuthenticatedUser("owner.delete@example.com");
    var boardId = await CreateBoard(ownerClient, "Board To Delete");

    var response = await ownerClient.DeleteAsync($"/api/boards/{boardId}/");
    LogHttp(nameof(DeleteBoard_AsOwner_ReturnsNoContent), response.StatusCode, HttpStatusCode.NoContent);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
}

[Fact]
public async Task DeleteBoard_AsOwner_BoardNoLongerAccessible()
{
    var (ownerClient, _) = await CreateAuthenticatedUser("owner.delete.gone@example.com");
    var boardId = await CreateBoard(ownerClient, "Board Gone After Delete");

    await ownerClient.DeleteAsync($"/api/boards/{boardId}/");

    var response = await ownerClient.GetAsync($"/api/boards/{boardId}/");
    LogHttp(nameof(DeleteBoard_AsOwner_BoardNoLongerAccessible), response.StatusCode, HttpStatusCode.NotFound);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}

[Fact]
public async Task DeleteBoard_AsMember_ReturnsForbidden()
{
    var (ownerClient, _) = await CreateAuthenticatedUser("owner.delete.member@example.com");
    var (memberClient, memberId) = await CreateAuthenticatedUser("member.delete@example.com");
    var boardId = await CreateBoard(ownerClient, "Member Cannot Delete");

    await AddBoardMember(boardId, memberId, "Member");

    var response = await memberClient.DeleteAsync($"/api/boards/{boardId}/");
    LogHttp(nameof(DeleteBoard_AsMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

[Fact]
public async Task DeleteBoard_AsNonMember_ReturnsForbidden()
{
    var (ownerClient, _) = await CreateAuthenticatedUser("owner.delete.nonmember@example.com");
    var (nonMemberClient, _) = await CreateAuthenticatedUser("nonmember.delete@example.com");
    var boardId = await CreateBoard(ownerClient, "Non Member Cannot Delete");

    var response = await nonMemberClient.DeleteAsync($"/api/boards/{boardId}/");
    LogHttp(nameof(DeleteBoard_AsNonMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

[Fact]
public async Task DeleteBoard_WithoutAuth_ReturnsUnauthorized()
{
    var unauthenticatedClient = _factory.CreateClient();

    var response = await unauthenticatedClient.DeleteAsync("/api/boards/123/");
    LogHttp(nameof(DeleteBoard_WithoutAuth_ReturnsUnauthorized), response.StatusCode, HttpStatusCode.Unauthorized);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task DeleteBoard_MissingBoard_ReturnsNotFound()
{
    var (client, _) = await CreateAuthenticatedUser("owner.delete.missing@example.com");

    var response = await client.DeleteAsync("/api/boards/999999/");

    LogHttp(nameof(DeleteBoard_MissingBoard_ReturnsNotFound), response.StatusCode, HttpStatusCode.Forbidden);
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    // No response body expected for Forbidden
}


    private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedUser(string email, string password = "Password123!")
    {
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

    private async Task AddCardToBoardColumn(int boardId, int columnPosition, string title)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var column = await db.Columns
            .Include(c => c.Cards)
            .FirstOrDefaultAsync(c => c.BoardId == boardId && c.Position == columnPosition);

        Assert.NotNull(column);
        column!.AddCard(title);
        await db.SaveChangesAsync();
    }

    private static async Task<BoardByIdResponse?> ReadBoardByIdResponse(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<BoardByIdResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static void LogHttp(string testName, HttpStatusCode actual, HttpStatusCode expected)
    {
        var statusCode = (int)actual;
        var statusColor = statusCode switch
        {
            >= 200 and < 300 => ConsoleColor.Green,
            >= 300 and < 400 => ConsoleColor.Yellow,
            >= 400 => ConsoleColor.Red,
            _ => ConsoleColor.White
        };

        if (actual != expected)
        {
            statusColor = ConsoleColor.Red;
        }
        TestConsole.FileHeader();

        Console.WriteLine(
            $"\n{testName} -> HTTP {TestConsole.Value((int)actual, statusColor)} ({TestConsole.Value(actual, statusColor)})");
    }

    private static string TrimForConsole(string text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text)) return "<empty>";

        var singleLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return singleLine.Length <= maxLength ? singleLine : $"{singleLine[..maxLength]}...";
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

        Console.WriteLine(
            $"\nBoardById DTO:" +
            $"\nId: {TestConsole.Value(dto.Id, ConsoleColor.Cyan)}" +
            $"\nName: {TestConsole.Value(dto.Name, ConsoleColor.Cyan)}" +
            $"\nOwnerId: {TestConsole.Value(dto.OwnerId, ConsoleColor.Cyan)}" +
            $"\nRole: {TestConsole.Value(dto.Role, ConsoleColor.Cyan)}" +
            $"\nMembers: {TestConsole.Value(memberList, ConsoleColor.Yellow)}" +
            $"\nColumns: {TestConsole.Value(columnList, ConsoleColor.Green)}\n");
    }

    private sealed record BoardByIdResponse(
        int Id,
        string Name,
        string OwnerId,
        string Role,
        List<MemberResponse> Members,
        List<ColumnResponse> Columns);

    private sealed record MemberResponse(string UserId, string Role);

    private sealed record ColumnResponse(int Id, string Name, int Position, List<CardResponse> Cards);

    private sealed record CardResponse(int Id, string Title);
    [Fact]
    public async Task DeleteBoard_WithColumnsAndCards_RemovesAllRelatedData()
    {
        // Arrange: create owner and board
        var (ownerClient, ownerId) = await CreateAuthenticatedUser("owner.delete.cascade@example.com");
        var boardId = await CreateBoard(ownerClient, "Cascade Delete Board");

        // Add a card to the first column (position 0)
        await AddCardToBoardColumn(boardId, 0, "Card to be deleted");

        // Print board, columns, and cards before deletion
        Console.WriteLine("\n========== [DeleteBoard_WithColumnsAndCards_RemovesAllRelatedData] ==========");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var boardBefore = await db.Boards.FindAsync(boardId);
            Console.WriteLine($"Board before delete: Id={boardBefore?.Id}, Name={boardBefore?.Name}");
            var columns = await db.Columns.Where(c => c.BoardId == boardId).ToListAsync();
            Console.WriteLine($"Columns before delete (count={columns.Count}): [" + string.Join(", ", columns.Select(c => $"{c.Id}:{c.Name}")) + "]");
            if (columns.Count == 0) Console.WriteLine("WARNING: No columns found before delete!");
            var cards = await db.Cards.Where(card => card.Column.BoardId == boardId).ToListAsync();
            Console.WriteLine($"Cards before delete (count={cards.Count}): [" + string.Join(", ", cards.Select(c => $"{c.Id}:{c.Title}")) + "]");
            if (cards.Count == 0) Console.WriteLine("WARNING: No cards found before delete!");
        }

        // Act: delete the board
        var response = await ownerClient.DeleteAsync($"/api/boards/{boardId}/");
        LogHttp(nameof(DeleteBoard_WithColumnsAndCards_RemovesAllRelatedData), response.StatusCode, HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert: board, columns, and cards are deleted
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = await db2.Boards.FindAsync(boardId);
        TestConsole.FileHeader();
        
        Console.WriteLine($"Deleted BoardId: {TestConsole.Value(boardId, ConsoleColor.Cyan)}");
        Console.WriteLine($"Board after delete: {TestConsole.Value(board == null ? "<null>" : board.Id.ToString(), ConsoleColor.Yellow)}");
        Assert.Null(board);

        var columnsAfter = await db2.Columns.Where(c => c.BoardId == boardId).ToListAsync();
        Console.WriteLine("Columns after delete: [" + string.Join(", ", columnsAfter.Select(c => $"{c.Id}:{c.Name}")) + "]");
        Assert.Empty(columnsAfter);

        var cardsAfter = await db2.Cards.Where(card => card.Column.BoardId == boardId).ToListAsync();
        Console.WriteLine("Cards after delete: [" + string.Join(", ", cardsAfter.Select(c => $"{c.Id}:{c.Title}")) + "]");
        Assert.Empty(cardsAfter);
    }
}
