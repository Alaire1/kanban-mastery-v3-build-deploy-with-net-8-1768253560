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

public class ColumnsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ColumnsEndpointTests(WebApplicationFactory<Program> factory)
    {
        var dbName = $"TestDb_Columns_{Guid.NewGuid()}";
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

    // --- Auth / board helpers ---

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

    private async Task AddCardToColumn(int columnId, string title)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var column = await db.Columns.FirstOrDefaultAsync(c => c.Id == columnId);
        Assert.NotNull(column);
        var card = column!.AddCard(title);
        db.Cards.Add(card);
        await db.SaveChangesAsync();
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

    private static void PrintColumnDto(string label, ColumnApiResponseDto dto)
    {
        TestConsole.FileHeader();
        Console.WriteLine(
            $"\n{label}:" +
            $"\nId: {TestConsole.Value(dto.Id, ConsoleColor.Cyan)}" +
            $"\nName: {TestConsole.Value(dto.Name, ConsoleColor.Cyan)}" +
            $"\nPosition: {TestConsole.Value(dto.Position, ConsoleColor.Cyan)}\n");
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
    public async Task ColumnLifecycle_AsMember_FullFlow()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var (memberClient, memberId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Columns Board");
        await AddBoardMember(boardId, memberId, "Member");

        // Create
        var createResponse = await memberClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Review", Position = 10 });
        LogHttp(nameof(ColumnLifecycle_AsMember_FullFlow) + " [create]", createResponse.StatusCode, HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ColumnApiResponseDto>();
        Assert.NotNull(created);
        Assert.Equal("Review", created!.Name);
        Assert.Equal(10, created.Position);
        PrintColumnDto("Created column", created);

        // Update
        var updateResponse = await memberClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/columns/{created.Id}",
            new { Name = "QA Review" });
        LogHttp(nameof(ColumnLifecycle_AsMember_FullFlow) + " [update]", updateResponse.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ColumnApiResponseDto>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated!.Id);
        Assert.Equal("QA Review", updated.Name);
        Assert.Equal(10, updated.Position);
        PrintColumnDto("Updated column", updated);

        // Delete
        var deleteResponse = await memberClient.DeleteAsync($"/api/boards/{boardId}/columns/{created.Id}");
        LogHttp(nameof(ColumnLifecycle_AsMember_FullFlow) + " [delete]", deleteResponse.StatusCode, HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stillExists = await db.Columns.AnyAsync(c => c.Id == created.Id && c.BoardId == boardId);
        Assert.False(stillExists);

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\nOwnerId: {TestConsole.Value(ownerId, ConsoleColor.Green)}" +
            $"\nMemberId: {TestConsole.Value(memberId, ConsoleColor.Yellow)}" +
            $"\nBoardId: {TestConsole.Value(boardId, ConsoleColor.Cyan)}" +
            $"\nColumnDeletedFromDb: {TestConsole.Value(!stillExists, ConsoleColor.Green)}\n");
    }

    [Fact]
    public async Task UpdateColumn_AsMember_ReturnsOk()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (memberClient, memberId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Update Member Board");
        await AddBoardMember(boardId, memberId, "Member");

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Original", Position = 10 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ColumnApiResponseDto>();

        var updateResponse = await memberClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/columns/{created!.Id}",
            new { Name = "Renamed" });
        LogHttp(nameof(UpdateColumn_AsMember_ReturnsOk), updateResponse.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteColumn_AsMember_ReturnsNoContent()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (memberClient, memberId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Delete Member Board");
        await AddBoardMember(boardId, memberId, "Member");

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Protected", Position = 10 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ColumnApiResponseDto>();

        var deleteResponse = await memberClient.DeleteAsync($"/api/boards/{boardId}/columns/{created!.Id}");
        LogHttp(nameof(DeleteColumn_AsMember_ReturnsNoContent), deleteResponse.StatusCode, HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateColumn_AsOwner_ReturnsOk()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Owner Update Board");

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Original", Position = 10 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ColumnApiResponseDto>();

        var updateResponse = await ownerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/columns/{created!.Id}",
            new { Name = "Renamed By Owner" });
        LogHttp(nameof(UpdateColumn_AsOwner_ReturnsOk), updateResponse.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteColumn_AsOwner_ReturnsNoContent()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Owner Delete Board");

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "To Delete", Position = 10 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ColumnApiResponseDto>();

        var deleteResponse = await ownerClient.DeleteAsync($"/api/boards/{boardId}/columns/{created!.Id}");
        LogHttp(nameof(DeleteColumn_AsOwner_ReturnsNoContent), deleteResponse.StatusCode, HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task CreateColumn_WithoutAuth_ReturnsUnauthorized()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Unauth Columns Board");

        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Review", Position = 10 });

        LogHttp(nameof(CreateColumn_WithoutAuth_ReturnsUnauthorized), response.StatusCode, HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateColumn_AsNonMember_ReturnsForbidden()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var (nonMemberClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Private Columns Board");

        var response = await nonMemberClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Review", Position = 10 });

        LogHttp(nameof(CreateColumn_AsNonMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateColumn_NonExistentBoard_ReturnsForbidden()
    {
        // Returns 403 rather than 404 intentionally — the API does not
        // reveal whether a board exists to non-members.
        var (client, _) = await CreateAuthenticatedUser();
        const int missingBoardId = 999_999;

        var response = await client.PostAsJsonAsync(
            $"/api/boards/{missingBoardId}/columns",
            new { Name = "Review", Position = 10 });

        LogHttp(nameof(CreateColumn_NonExistentBoard_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateColumn_InvalidName_ReturnsBadRequest()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Validation Board");

        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "   ", Position = 10 });

        LogHttp(nameof(CreateColumn_InvalidName_ReturnsBadRequest), response.StatusCode, HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorBody = await response.Content.ReadAsStringAsync();
        PrintErrorResponse("CreateColumn invalid name", errorBody);

        using var doc = JsonDocument.Parse(errorBody);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors), "Missing 'errors'");
        Assert.True(errors.TryGetProperty("Name", out var nameErrors), "Missing 'Name' in errors");

        var errorList = nameErrors.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("Column name cannot be empty", errorList);
    }

    [Fact]
    public async Task CreateColumn_DuplicatePosition_ReturnsConflict()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Duplicate Position Board");

        // Position 0 is occupied by the board's default seed column.
        // If board seeding changes, update this position accordingly.
        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Review", Position = 0 });

        LogHttp(nameof(CreateColumn_DuplicatePosition_ReturnsConflict), response.StatusCode, HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var errorBody = await response.Content.ReadAsStringAsync();
        PrintErrorResponse("CreateColumn duplicate position", errorBody);
        Assert.Contains("already exists at this position", errorBody);
    }

    [Fact]
    public async Task UpdateColumn_MissingColumn_ReturnsNotFound()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Update Missing Column Board");

        var response = await ownerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/columns/999999",
            new { Name = "Renamed" });

        LogHttp(nameof(UpdateColumn_MissingColumn_ReturnsNotFound), response.StatusCode, HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var errorBody = await response.Content.ReadAsStringAsync();
        PrintErrorResponse("UpdateColumn missing column", errorBody);
        Assert.Contains("Column not found", errorBody);
    }

    [Fact]
    public async Task UpdateColumn_RenameOnly_LeavesPositionUnchanged()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Rename Only Board");

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Review", Position = 10 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ColumnApiResponseDto>();
        Assert.NotNull(created);

        var updateResponse = await ownerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/columns/{created!.Id}",
            new { Name = "Review Updated" });
        LogHttp(nameof(UpdateColumn_RenameOnly_LeavesPositionUnchanged), updateResponse.StatusCode, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<ColumnApiResponseDto>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated!.Id);
        Assert.Equal("Review Updated", updated.Name);
        Assert.Equal(10, updated.Position);
    }

    [Fact]
    public async Task DeleteColumn_WithExistingCards_ReturnsBadRequest()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Delete Column With Cards Board");

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Needs Cleanup", Position = 10 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ColumnApiResponseDto>();
        Assert.NotNull(created);

        await AddCardToColumn(created!.Id, "Card that blocks deletion");

        var deleteResponse = await ownerClient.DeleteAsync($"/api/boards/{boardId}/columns/{created.Id}");
        LogHttp(nameof(DeleteColumn_WithExistingCards_ReturnsBadRequest), deleteResponse.StatusCode, HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        var errorBody = await deleteResponse.Content.ReadAsStringAsync();
        PrintErrorResponse("DeleteColumn with cards", errorBody);
        Assert.Contains("Cannot delete column with existing cards.", errorBody);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Columns.AnyAsync(c => c.Id == created.Id && c.BoardId == boardId));
        Assert.True(await db.Cards.AnyAsync(card => card.ColumnId == created.Id));
    }

    [Fact]
    public async Task DeleteColumn_AfterCardsRemoved_ReturnsNoContent()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Delete After Cleanup Board");

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Cleanup Me", Position = 10 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ColumnApiResponseDto>();
        Assert.NotNull(created);

        await AddCardToColumn(created!.Id, "Card to remove first");

        var firstDelete = await ownerClient.DeleteAsync($"/api/boards/{boardId}/columns/{created.Id}");
        LogHttp(nameof(DeleteColumn_AfterCardsRemoved_ReturnsNoContent) + " [blocked]", firstDelete.StatusCode, HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.BadRequest, firstDelete.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cards = await db.Cards.Where(c => c.ColumnId == created.Id).ToListAsync();
            db.Cards.RemoveRange(cards);
            await db.SaveChangesAsync();
        }

        var secondDelete = await ownerClient.DeleteAsync($"/api/boards/{boardId}/columns/{created.Id}");
        LogHttp(nameof(DeleteColumn_AfterCardsRemoved_ReturnsNoContent) + " [success]", secondDelete.StatusCode, HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NoContent, secondDelete.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await verifyDb.Columns.AnyAsync(c => c.Id == created.Id));
        Assert.False(await verifyDb.Cards.AnyAsync(c => c.ColumnId == created.Id));
    }

    [Fact]
    public async Task DeleteColumn_MissingColumn_ReturnsNotFound()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerClient, "Delete Missing Column Board");

        var response = await ownerClient.DeleteAsync($"/api/boards/{boardId}/columns/999999");

        LogHttp(nameof(DeleteColumn_MissingColumn_ReturnsNotFound), response.StatusCode, HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var errorBody = await response.Content.ReadAsStringAsync();
        PrintErrorResponse("DeleteColumn missing column", errorBody);
        Assert.Contains("Column not found", errorBody);
    }
}