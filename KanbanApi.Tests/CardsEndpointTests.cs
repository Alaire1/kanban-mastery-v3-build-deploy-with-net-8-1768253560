using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KanbanApi;
using KanbanApi.Data;
using KanbanApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KanbanApi.Tests;

public class CardsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CardsEndpointTests(WebApplicationFactory<Program> factory)
    {
        var dbName = $"TestDb_Cards_{Guid.NewGuid()}";
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

    private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedUser(
        string? email = null, string password = "Password123!")
    {
        email ??= $"user_{Guid.NewGuid():N}@example.com";
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/register", new { email, password });
        var loginResp = await client.PostAsJsonAsync("/login", new { email, password });
        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var profileResp = await client.GetAsync("/api/users/me");
        var profile = await profileResp.Content.ReadFromJsonAsync<UserProfileResponse>();
        return (client, profile!.Id);
    }

    private async Task<int> CreateBoard(string ownerId, string boardName = "Test Board")
    {
        using var scope = _factory.Services.CreateScope();
        var boardService = scope.ServiceProvider.GetRequiredService<IBoardService>();
        var board = await boardService.CreateBoardAsync(boardName, ownerId);
        return board.Id;
    }

    private sealed record CardResponse(int Id, string Title, int ColumnId);

    [Fact]
    public async Task CreateCard_WithValidData_ReturnsCreatedCard()
    {
        Console.WriteLine("\n");
        TestConsole.FileHeader();

        var (client, userId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(userId);

        int columnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            columnId = db.Columns.First(c => c.BoardId == boardId).Id;
        }

        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/cards",
            new { Title = "Test Card", Description = "Test Description", ColumnId = columnId });
        var responseContent = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"BoardId: {TestConsole.Value(boardId, ConsoleColor.Cyan)}");
        Console.WriteLine($"UserId: {TestConsole.Value(userId, ConsoleColor.Cyan)}");
        Console.WriteLine($"ColumnId: {TestConsole.Value(columnId, ConsoleColor.Cyan)}");
        Console.WriteLine($"Status: {TestConsole.Value(response.StatusCode, ConsoleColor.Yellow)}");
        Console.WriteLine($"Location: {TestConsole.Value(response.Headers.Location, ConsoleColor.Green)}");
        Console.WriteLine($"Response Content: {TestConsole.Value(responseContent, ConsoleColor.Magenta)}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        TestConsole.LabelValue("Card creation successful with status code: ", response.StatusCode, ConsoleColor.Green);
        Console.WriteLine("\n");

        if (string.IsNullOrWhiteSpace(responseContent))
            throw new Exception($"Response content is empty. Status: {response.StatusCode}, Location: {response.Headers.Location}");

        var card = JsonSerializer.Deserialize<CardResponse>(responseContent, JsonOptions);
        Assert.NotNull(card);
        Assert.Equal("Test Card", card!.Title);
        Assert.True(card.Id > 0);
    }

    [Fact]
    public async Task CardLifecycle_FullFlow_WorksCorrectly()
    {
        TestConsole.FileHeader();

        var (client, userId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(userId);

        int columnId1, columnId2;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var columns = db.Columns.Where(c => c.BoardId == boardId).OrderBy(c => c.Position).ToList();
            columnId1 = columns[0].Id;
            columnId2 = columns.Count > 1
                ? columns[1].Id
                : throw new Exception("Test board must have at least 2 columns");
        }

        // 1. Create card
        var createResp = await client.PostAsJsonAsync($"/api/boards/{boardId}/cards",
            new { Title = "Lifecycle Card", Description = "desc", ColumnId = columnId1 });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var card = JsonSerializer.Deserialize<CardResponse>(await createResp.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(card);
        Console.WriteLine($"Created Card Id: {TestConsole.Value(card!.Id, ConsoleColor.Cyan)}");
        Console.WriteLine($"Created Card Title: {TestConsole.Value(card.Title, ConsoleColor.Cyan)}");
        Console.WriteLine($"Created Card ColumnId: {TestConsole.Value(card.ColumnId, ConsoleColor.Cyan)}");

        // 2. Update card title
        var updateResp = await client.PutAsJsonAsync($"/api/boards/{boardId}/cards/{card.Id}",
            new { Title = "Updated Title", Description = "desc", ColumnId = columnId1 });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = JsonSerializer.Deserialize<CardResponse>(await updateResp.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated!.Title);
        Assert.Equal(columnId1, updated.ColumnId);
        Console.WriteLine($"Updated Card Title: {TestConsole.Value(updated.Title, ConsoleColor.Yellow)}");
        Console.WriteLine($"Updated Card ColumnId: {TestConsole.Value(updated.ColumnId, ConsoleColor.Yellow)}");

        // 3. Move card to another column
        var moveResp = await client.PutAsJsonAsync($"/api/boards/{boardId}/cards/{card.Id}",
            new { Title = "Updated Title", Description = "desc", ColumnId = columnId2 });
        Assert.Equal(HttpStatusCode.OK, moveResp.StatusCode);
        var moved = JsonSerializer.Deserialize<CardResponse>(await moveResp.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(moved);
        Assert.Equal(columnId2, moved!.ColumnId);
        Console.WriteLine($"Moved Card ColumnId: {TestConsole.Value(moved.ColumnId, ConsoleColor.Magenta)}");

        // 4. Delete card
        var deleteResp = await client.DeleteAsync($"/api/boards/{boardId}/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
        Console.WriteLine($"Card deleted, status: {TestConsole.Value(deleteResp.StatusCode, ConsoleColor.Green)}");
        Console.WriteLine("\n");
    }

    [Fact]
    public async Task CardEndpoints_NonMember_Forbidden()
    {
        TestConsole.FileHeader();

        var (ownerClient, ownerId) = await CreateAuthenticatedUser();
        var (nonMemberClient, _) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(ownerId);

        int columnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            columnId = db.Columns.First(c => c.BoardId == boardId).Id;
        }

        // Owner creates a card — assert it actually succeeded before proceeding
        var createResp = await ownerClient.PostAsJsonAsync($"/api/boards/{boardId}/cards",
            new { Title = "Card", Description = "desc", ColumnId = columnId });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var card = JsonSerializer.Deserialize<CardResponse>(await createResp.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(card);
        Console.WriteLine($"Owner created Card Id: {TestConsole.Value(card!.Id, ConsoleColor.Cyan)}");

        // Non-member tries create
        var forbiddenCreate = await nonMemberClient.PostAsJsonAsync($"/api/boards/{boardId}/cards",
            new { Title = "X", Description = "Y", ColumnId = columnId });
        Console.WriteLine($"Non-member create status: {TestConsole.Value(forbiddenCreate.StatusCode, ConsoleColor.Yellow)}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        // Non-member tries update
        var forbiddenUpdate = await nonMemberClient.PutAsJsonAsync($"/api/boards/{boardId}/cards/{card.Id}",
            new { Title = "X", Description = "Y", ColumnId = columnId });
        Console.WriteLine($"Non-member update status: {TestConsole.Value(forbiddenUpdate.StatusCode, ConsoleColor.Yellow)}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUpdate.StatusCode);

        // Non-member tries delete
        var forbiddenDelete = await nonMemberClient.DeleteAsync($"/api/boards/{boardId}/cards/{card.Id}");
        Console.WriteLine($"Non-member delete status: {TestConsole.Value(forbiddenDelete.StatusCode, ConsoleColor.Yellow)}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        Console.WriteLine("\n");
    }

    [Fact]
    public async Task CreateCard_WithMissingColumn_ReturnsNotFound()
    {
        var (client, userId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(userId);

        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/cards",
            new { Title = "Orphan Card", Description = "desc", ColumnId = 999_999 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Column not found", body);
    }

    [Fact]
    public async Task UpdateCard_NonExistentCard_ReturnsNotFound()
    {
        var (client, userId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(userId);

        int columnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            columnId = db.Columns.First(c => c.BoardId == boardId).Id;
        }

        var response = await client.PutAsJsonAsync($"/api/boards/{boardId}/cards/999999",
            new { Title = "Updated", Description = "desc", ColumnId = columnId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Card not found", body);
    }

    [Fact]
    public async Task DeleteCard_NonExistentCard_ReturnsNotFound()
    {
        var (client, userId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(userId);

        var response = await client.DeleteAsync($"/api/boards/{boardId}/cards/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Card not found", body);
    }

    [Fact]
    public async Task UpdateCard_InvalidDto_ReturnsBadRequest()
    {
        var (client, userId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(userId);

        int columnId;
        int cardId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            columnId = db.Columns.First(c => c.BoardId == boardId).Id;
        }

        var createResp = await client.PostAsJsonAsync($"/api/boards/{boardId}/cards",
            new { Title = "Valid Title", Description = "desc", ColumnId = columnId });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = JsonSerializer.Deserialize<CardResponse>(await createResp.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(created);
        cardId = created!.Id;

        var updateResp = await client.PutAsJsonAsync($"/api/boards/{boardId}/cards/{cardId}",
            new { Title = "   ", Description = "desc", ColumnId = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, updateResp.StatusCode);
        var body = await updateResp.Content.ReadAsStringAsync();
        Assert.Contains("errors", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCard_InvalidColumnId_ReturnsBadRequest()
    {
        var (client, userId) = await CreateAuthenticatedUser();
        var boardId = await CreateBoard(userId);

        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/cards",
            new { Title = "ValidTitle", Description = "desc", ColumnId = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ColumnId", body, StringComparison.OrdinalIgnoreCase);
    }
}