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
    }
}
