using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KanbanApi;
using KanbanApi.Data;
using KanbanApi.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KanbanApi.Tests;

public class E2ESeedUserBoardsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public E2ESeedUserBoardsTests(WebApplicationFactory<Program> factory)
    {
        var dbName = $"TestDb_E2ESeed_{Guid.NewGuid()}";

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
            });
        });

        _client = _factory.CreateClient();
    }

    private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedUser(
        string email,
        string password = E2ESeedCredentials.Password)
    {
        var client = _factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/register", new { email, password });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var meResponse = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        using var meDoc = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
        var userId = meDoc.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(userId));

        return (client, userId!);
    }

    private static async Task<int> CreateBoard(HttpClient client, string boardName)
    {
        var response = await client.PostAsJsonAsync("/api/boards", new { boardName });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var board = await response.Content.ReadFromJsonAsync<BoardDto>();
        Assert.NotNull(board);
        return board!.Id;
    }

    private static async Task<HttpStatusCode> AddMember(HttpClient client, int boardId, string userId)
    {
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/members", new { userId });
        return response.StatusCode;
    }

    private static async Task<HashSet<string>> GetBoardNames(HttpClient client)
    {
        var response = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.TryGetProperty("boards", out var boards) && boards.ValueKind == JsonValueKind.Array)
        {
            foreach (var board in boards.EnumerateArray())
            {
                if (board.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                {
                    var name = nameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                }
            }
        }

        return names;
    }

    private static async Task<List<int>> GetColumnIds(HttpClient client, int boardId)
    {
        var response = await client.GetAsync($"/api/boards/{boardId}/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var columns = doc.RootElement.GetProperty("columns");
        var ids = new List<int>();

        foreach (var column in columns.EnumerateArray())
        {
            if (column.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.Number)
            {
                ids.Add(idElement.GetInt32());
            }
        }

        return ids;
    }

    private static async Task<HttpStatusCode> CreateCard(HttpClient client, int boardId, int columnId, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/cards", new
        {
            title,
            description = $"Seeded card: {title}",
            columnId
        });

        return response.StatusCode;
    }

    private static async Task<int> GetTotalCardCount(HttpClient client, int boardId)
    {
        var response = await client.GetAsync($"/api/boards/{boardId}/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var columns = doc.RootElement.GetProperty("columns");
        var count = 0;

        foreach (var column in columns.EnumerateArray())
        {
            if (column.TryGetProperty("cards", out var cards) && cards.ValueKind == JsonValueKind.Array)
            {
                count += cards.GetArrayLength();
            }
        }

        return count;
    }

    private static async Task<int> GetCardCountInColumn(HttpClient client, int boardId, int columnId)
    {
        var response = await client.GetAsync($"/api/boards/{boardId}/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var columns = doc.RootElement.GetProperty("columns");

        foreach (var column in columns.EnumerateArray())
        {
            if (!column.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.Number)
                continue;

            if (idElement.GetInt32() != columnId)
                continue;

            if (!column.TryGetProperty("cards", out var cards) || cards.ValueKind != JsonValueKind.Array)
                return 0;

            return cards.GetArrayLength();
        }

        return 0;
    }

    private static async Task<int> GetCardIdByTitle(HttpClient client, int boardId, string title)
    {
        var response = await client.GetAsync($"/api/boards/{boardId}/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var columns = doc.RootElement.GetProperty("columns");

        foreach (var column in columns.EnumerateArray())
        {
            if (!column.TryGetProperty("cards", out var cards) || cards.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var card in cards.EnumerateArray())
            {
                if (!card.TryGetProperty("title", out var titleElement) || titleElement.ValueKind != JsonValueKind.String)
                    continue;

                if (!string.Equals(titleElement.GetString(), title, StringComparison.Ordinal))
                    continue;

                if (card.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.Number)
                    return idElement.GetInt32();
            }
        }

        return 0;
    }

    private async Task SetUserDisplayName(string userId, string displayName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FindAsync(userId);
        Assert.NotNull(user);
        user!.DisplayName = displayName;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task RegisterLogin_AndSeedBoards_ForPlaywrightUser()
    {
        // Register (or keep existing if duplicate)
        var registerResponse = await _client.PostAsJsonAsync("/register", new
        {
            email = E2ESeedCredentials.Email,
            password = E2ESeedCredentials.Password
        });

        Assert.True(
            registerResponse.StatusCode == HttpStatusCode.OK ||
            registerResponse.StatusCode == HttpStatusCode.BadRequest,
            $"Unexpected register status: {registerResponse.StatusCode}");

        // Login with the same credentials
        var loginResponse = await _client.PostAsJsonAsync("/login", new
        {
            email = E2ESeedCredentials.Email,
            password = E2ESeedCredentials.Password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Fetch existing boards for this user
        var meResponse = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var meJson = await meResponse.Content.ReadAsStringAsync();
        var boardNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var doc = JsonDocument.Parse(meJson))
        {
            if (doc.RootElement.TryGetProperty("boards", out var boardsElement) && boardsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var board in boardsElement.EnumerateArray())
                {
                    if (board.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            boardNames.Add(name);
                        }
                    }
                }
            }
        }

        // Add missing boards
        foreach (var boardName in E2ESeedCredentials.BoardNames.Where(name => !boardNames.Contains(name)))
        {
            var createBoardResponse = await _client.PostAsJsonAsync("/api/boards", new { boardName });
            Assert.Equal(HttpStatusCode.Created, createBoardResponse.StatusCode);
        }

        // Add cards into one column for the named seeded boards.
        const int minCardsInFirstColumn = 3;

        var seededBoardsResponse = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, seededBoardsResponse.StatusCode);

        using (var seededBoardsDoc = JsonDocument.Parse(await seededBoardsResponse.Content.ReadAsStringAsync()))
        {
            if (seededBoardsDoc.RootElement.TryGetProperty("boards", out var boardsElement) && boardsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var board in boardsElement.EnumerateArray())
                {
                    if (!board.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                        continue;

                    var boardName = nameElement.GetString();
                    if (string.IsNullOrWhiteSpace(boardName) || !E2ESeedCredentials.BoardNames.Contains(boardName, StringComparer.OrdinalIgnoreCase))
                        continue;

                    if (!board.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.Number)
                        continue;

                    var boardId = idElement.GetInt32();

                    var columnIds = await GetColumnIds(_client, boardId);
                    if (columnIds.Count == 0)
                        continue;

                    var firstColumnId = columnIds[0];
                    var existingCardsInFirstColumn = await GetCardCountInColumn(_client, boardId, firstColumnId);
                    var cardsToAdd = Math.Max(0, minCardsInFirstColumn - existingCardsInFirstColumn);

                    // Add only the missing cards so re-runs are idempotent.
                    for (var i = 1; i <= cardsToAdd; i++)
                    {
                        Assert.Equal(
                            HttpStatusCode.Created,
                            await CreateCard(_client, boardId, firstColumnId, $"Seed {boardId} {i}"));
                    }
                }
            }
        }

        // Verify seeded boards exist
        var verifyMeResponse = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, verifyMeResponse.StatusCode);

        var verifyJson = await verifyMeResponse.Content.ReadAsStringAsync();
        using var verifyDoc = JsonDocument.Parse(verifyJson);

        var verifyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (verifyDoc.RootElement.TryGetProperty("boards", out var verifyBoards) && verifyBoards.ValueKind == JsonValueKind.Array)
        {
            foreach (var board in verifyBoards.EnumerateArray())
            {
                if (board.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                {
                    var name = nameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        verifyNames.Add(name);
                    }
                }
            }
        }

        foreach (var expected in E2ESeedCredentials.BoardNames)
        {
            Assert.Contains(expected, verifyNames);
        }

        // Verify each seeded board has multiple cards in its first column.
        using (var seededBoardsDoc = JsonDocument.Parse(verifyJson))
        {
            if (seededBoardsDoc.RootElement.TryGetProperty("boards", out var boardsElement) && boardsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var board in boardsElement.EnumerateArray())
                {
                    if (!board.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.Number)
                        continue;

                    var boardId = idElement.GetInt32();
                    var columnIds = await GetColumnIds(_client, boardId);
                    Assert.NotEmpty(columnIds);

                    var firstColumnCardCount = await GetCardCountInColumn(_client, boardId, columnIds[0]);
                    Assert.True(firstColumnCardCount >= 3,
                        $"Expected at least 3 seeded cards in first column for board {boardId}, but found {firstColumnCardCount}.");

                    var totalCards = await GetTotalCardCount(_client, boardId);
                    Assert.True(totalCards > 0, $"Expected seeded cards in board {boardId}, but found none.");
                }
            }
        }

    }

    [Fact]
    public async Task TwoUsers_CreateBoards_AddEachOther_AndDuplicateAddReturnsConflict()
    {
        var (userOneClient, userOneId) = await CreateAuthenticatedUser(
            E2ESeedCredentials.UserOneEmail,
            E2ESeedCredentials.UserOnePassword);

        var (userTwoClient, userTwoId) = await CreateAuthenticatedUser(
            E2ESeedCredentials.UserTwoEmail,
            E2ESeedCredentials.UserTwoPassword);

        var userTwoBoardA = await CreateBoard(userTwoClient, "User2 Board A");
        var userTwoBoardB = await CreateBoard(userTwoClient, "User2 Board B");

        Assert.Equal(HttpStatusCode.Created, await AddMember(userTwoClient, userTwoBoardA, userOneId));
        Assert.Equal(HttpStatusCode.Conflict, await AddMember(userTwoClient, userTwoBoardA, userOneId));
        Assert.Equal(HttpStatusCode.Created, await AddMember(userTwoClient, userTwoBoardB, userOneId));

        var userOneBoardA = await CreateBoard(userOneClient, "User1 Board A");
        var userOneBoardB = await CreateBoard(userOneClient, "User1 Board B");

        Assert.Equal(HttpStatusCode.Created, await AddMember(userOneClient, userOneBoardA, userTwoId));
        Assert.Equal(HttpStatusCode.Conflict, await AddMember(userOneClient, userOneBoardA, userTwoId));
        Assert.Equal(HttpStatusCode.Created, await AddMember(userOneClient, userOneBoardB, userTwoId));

        // Seed cards in a couple of boards to ensure board payloads include nested cards for each user.
        var userTwoBoardAColumns = await GetColumnIds(userTwoClient, userTwoBoardA);
        Assert.NotEmpty(userTwoBoardAColumns);
        Assert.Equal(HttpStatusCode.Created, await CreateCard(userTwoClient, userTwoBoardA, userTwoBoardAColumns[0], "User2 A Card 1"));

        var userOneBoardAColumns = await GetColumnIds(userOneClient, userOneBoardA);
        Assert.NotEmpty(userOneBoardAColumns);
        Assert.Equal(HttpStatusCode.Created, await CreateCard(userOneClient, userOneBoardA, userOneBoardAColumns[0], "User1 A Card 1"));

        var userOneBoardNames = await GetBoardNames(userOneClient);
        var userTwoBoardNames = await GetBoardNames(userTwoClient);

        Assert.Contains("User1 Board A", userOneBoardNames);
        Assert.Contains("User1 Board B", userOneBoardNames);
        Assert.Contains("User2 Board A", userOneBoardNames);
        Assert.Contains("User2 Board B", userOneBoardNames);

        Assert.Contains("User2 Board A", userTwoBoardNames);
        Assert.Contains("User2 Board B", userTwoBoardNames);
        Assert.Contains("User1 Board A", userTwoBoardNames);
        Assert.Contains("User1 Board B", userTwoBoardNames);

        Assert.True(await GetTotalCardCount(userTwoClient, userTwoBoardA) > 0);
        Assert.True(await GetTotalCardCount(userOneClient, userOneBoardA) > 0);
    }

    [Fact]
    public async Task E2E_SeededUsers_CardsAssignEndpoint_AssignsCard_AndBoardPayloadShowsHoverFields()
    {
        // Use known seeded users so UI/e2e runs can rely on deterministic credentials.
        var (userOneClient, _) = await CreateAuthenticatedUser(
            E2ESeedCredentials.UserOneEmail,
            E2ESeedCredentials.UserOnePassword);

        var (_, userTwoId) = await CreateAuthenticatedUser(
            E2ESeedCredentials.UserTwoEmail,
            E2ESeedCredentials.UserTwoPassword);

        const string expectedDisplayName = "Playwright User Two";
        await SetUserDisplayName(userTwoId, expectedDisplayName);

        var boardId = await CreateBoard(userOneClient, $"E2E Assign Board {Guid.NewGuid():N}"[..28]);

        var addMemberStatus = await AddMember(userOneClient, boardId, userTwoId);
        Assert.Equal(HttpStatusCode.Created, addMemberStatus);

        var columnIds = await GetColumnIds(userOneClient, boardId);
        Assert.NotEmpty(columnIds);

        const string cardTitle = "E2E Assign Target Card";
        var createCardStatus = await CreateCard(userOneClient, boardId, columnIds[0], cardTitle);
        Assert.Equal(HttpStatusCode.Created, createCardStatus);

        var cardId = await GetCardIdByTitle(userOneClient, boardId, cardTitle);
        Assert.True(cardId > 0, "Expected created card to be discoverable by title.");

        // This call explicitly uses CardsAssignEndpoint:
        // PUT /api/boards/{boardId}/cards/{cardId}/assign
        var assignResponse = await userOneClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/cards/{cardId}/assign",
            new { userId = userTwoId });
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        var assignDto = await assignResponse.Content.ReadFromJsonAsync<AssignCardResponseDto>();
        Assert.NotNull(assignDto);
        Assert.Equal(cardId, assignDto!.CardId);
        Assert.Equal(userTwoId, assignDto.UserId);

        var boardResponse = await userOneClient.GetAsync($"/api/boards/{boardId}/");
        Assert.Equal(HttpStatusCode.OK, boardResponse.StatusCode);

        using var boardDoc = JsonDocument.Parse(await boardResponse.Content.ReadAsStringAsync());
        var columns = boardDoc.RootElement.GetProperty("columns");

        JsonElement assignedCard = default;
        var found = false;

        foreach (var column in columns.EnumerateArray())
        {
            if (!column.TryGetProperty("cards", out var cards) || cards.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var card in cards.EnumerateArray())
            {
                if (card.TryGetProperty("id", out var idElement)
                    && idElement.ValueKind == JsonValueKind.Number
                    && idElement.GetInt32() == cardId)
                {
                    assignedCard = card;
                    found = true;
                    break;
                }
            }

            if (found) break;
        }

        Assert.True(found, "Expected assigned card to be present in board payload.");

        var assignedUserId = assignedCard.GetProperty("assignedUserId").GetString();
        var assigneeUserName = assignedCard.GetProperty("assigneeUserName").GetString();
        var assigneeDisplayName = assignedCard.GetProperty("assigneeDisplayName").GetString();

        Assert.Equal(userTwoId, assignedUserId);
        Assert.False(string.IsNullOrWhiteSpace(assigneeUserName));
        Assert.Equal(expectedDisplayName, assigneeDisplayName);
    }
}
