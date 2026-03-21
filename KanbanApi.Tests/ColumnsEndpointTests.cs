using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    public async Task Member_CanCreateUpdateAndDeleteColumns()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedUser("owner.columns@example.com");
        var (memberClient, memberId) = await CreateAuthenticatedUser("member.columns@example.com");

        var boardId = await CreateBoard(ownerClient, "Columns Board");
        await AddBoardMember(boardId, memberId, "Member");

        var createResponse = await memberClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Review", Position = 10 });

        LogHttp("CreateColumn_AsMember", createResponse.StatusCode, HttpStatusCode.Created);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ColumnDto>();
        Assert.NotNull(created);
        Assert.Equal("Review", created!.Name);
        Assert.Equal(10, created.Position);

        PrintColumnDto("Created Column DTO", created);

        var updateResponse = await memberClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/columns/{created.Id}",
            new { Name = "QA Review", Position = 11 });

        LogHttp("UpdateColumn_AsMember", updateResponse.StatusCode, HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ColumnDto>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated!.Id);
        Assert.Equal("QA Review", updated.Name);
        Assert.Equal(11, updated.Position);

        PrintColumnDto("Updated Column DTO", updated);

        var deleteResponse = await memberClient.DeleteAsync(
            $"/api/boards/{boardId}/columns/{created.Id}");

        LogHttp("DeleteColumn_AsMember", deleteResponse.StatusCode, HttpStatusCode.NoContent);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stillExists = await db.Columns.AnyAsync(c => c.Id == created.Id && c.BoardId == boardId);
        Assert.False(stillExists);

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\nTest users and board:" +
            $"\nOwnerId: {TestConsole.Value(ownerId, ConsoleColor.Green)}" +
            $"\nMemberId: {TestConsole.Value(memberId, ConsoleColor.Yellow)}" +
            $"\nBoardId: {TestConsole.Value(boardId, ConsoleColor.Cyan)}" +
            $"\nColumnDeletedFromDb: {TestConsole.Value(!stillExists, ConsoleColor.Green)}\n");
    }

    [Fact]
    public async Task CreateColumn_WithoutAuth_ReturnsUnauthorized()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.columns.unauth@example.com");
        var boardId = await CreateBoard(ownerClient, "Unauth Columns Board");

        var unauthenticatedClient = _factory.CreateClient();
        var response = await unauthenticatedClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Review", Position = 10 });

        LogHttp(nameof(CreateColumn_WithoutAuth_ReturnsUnauthorized), response.StatusCode, HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateColumn_AsNonMember_ReturnsForbidden()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.columns.forbidden@example.com");
        var (nonMemberClient, _) = await CreateAuthenticatedUser("nonmember.columns.forbidden@example.com");
        var boardId = await CreateBoard(ownerClient, "Private Columns Board");

        var response = await nonMemberClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Review", Position = 10 });

        LogHttp(nameof(CreateColumn_AsNonMember_ReturnsForbidden), response.StatusCode, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateColumn_MissingBoard_ReturnsNotFound()
    {
        var (client, _) = await CreateAuthenticatedUser("member.columns.notfound@example.com");

        const int missingBoardId = 999_999;
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{missingBoardId}/columns",
            new { Name = "Review", Position = 10 });

        LogHttp(nameof(CreateColumn_MissingBoard_ReturnsNotFound), response.StatusCode, HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var errorBody = await response.Content.ReadAsStringAsync();
        PrintErrorResponse("CreateColumn missing board message", errorBody);
        Assert.Contains("Board not found", errorBody);
    }

    [Fact]
    public async Task CreateColumn_InvalidName_ReturnsBadRequest()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.columns.invalidname@example.com");
        var boardId = await CreateBoard(ownerClient, "Validation Board");

        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "   ", Position = 10 });

        LogHttp(nameof(CreateColumn_InvalidName_ReturnsBadRequest), response.StatusCode, HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorBody = await response.Content.ReadAsStringAsync();
        PrintErrorResponse("CreateColumn invalid name message", errorBody);
        Assert.Contains("Column name cannot be empty", errorBody);
    }

    [Fact]
    public async Task CreateColumn_DuplicatePosition_ReturnsConflict()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.columns.duplicate@example.com");
        var boardId = await CreateBoard(ownerClient, "Duplicate Position Board");

        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Review", Position = 0 });

        LogHttp(nameof(CreateColumn_DuplicatePosition_ReturnsConflict), response.StatusCode, HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var errorBody = await response.Content.ReadAsStringAsync();
        PrintErrorResponse("CreateColumn duplicate position message", errorBody);
        Assert.Contains("already exists at this position", errorBody);
    }

    [Fact]
    public async Task UpdateColumn_MissingColumn_ReturnsNotFound()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.columns.update.notfound@example.com");
        var boardId = await CreateBoard(ownerClient, "Update Missing Column Board");

        var response = await ownerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/columns/999999",
            new { Name = "Renamed", Position = 10 });

        LogHttp(nameof(UpdateColumn_MissingColumn_ReturnsNotFound), response.StatusCode, HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var errorBody = await response.Content.ReadAsStringAsync();
        PrintErrorResponse("UpdateColumn missing column message", errorBody);
        Assert.Contains("Column not found", errorBody);
    }

    [Fact]
    public async Task UpdateColumn_DuplicatePosition_ReturnsConflict()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.columns.update.duplicate@example.com");
        var boardId = await CreateBoard(ownerClient, "Update Duplicate Position Board");

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/columns",
            new { Name = "Review", Position = 10 });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ColumnDto>();
        Assert.NotNull(created);

        var updateResponse = await ownerClient.PutAsJsonAsync(
            $"/api/boards/{boardId}/columns/{created!.Id}",
            new { Name = "Review Updated", Position = 1 });

        LogHttp(nameof(UpdateColumn_DuplicatePosition_ReturnsConflict), updateResponse.StatusCode, HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);

        var errorBody = await updateResponse.Content.ReadAsStringAsync();
        PrintErrorResponse("UpdateColumn duplicate position message", errorBody);
        Assert.Contains("already exists at this position", errorBody);
    }

    [Fact]
    public async Task DeleteColumn_MissingColumn_ReturnsNotFound()
    {
        var (ownerClient, _) = await CreateAuthenticatedUser("owner.columns.delete.notfound@example.com");
        var boardId = await CreateBoard(ownerClient, "Delete Missing Column Board");

        var response = await ownerClient.DeleteAsync($"/api/boards/{boardId}/columns/999999");

        LogHttp(nameof(DeleteColumn_MissingColumn_ReturnsNotFound), response.StatusCode, HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var errorBody = await response.Content.ReadAsStringAsync();
        PrintErrorResponse("DeleteColumn missing column message", errorBody);
        Assert.Contains("Column not found", errorBody);
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

    private static void LogHttp(string operation, HttpStatusCode actual, HttpStatusCode expected)
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
            statusColor = ConsoleColor.Red;

        TestConsole.FileHeader();
        Console.WriteLine(
            $"\n{operation} -> HTTP {TestConsole.Value((int)actual, statusColor)} ({TestConsole.Value(actual, statusColor)})");
    }

    private static void PrintColumnDto(string label, ColumnDto dto)
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
        Console.WriteLine(
            $"\n{label}: {TestConsole.Value(TrimForConsole(errorBody), ConsoleColor.Yellow)}\n");
    }

    private static string TrimForConsole(string text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text)) return "<empty>";

        var singleLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return singleLine.Length <= maxLength ? singleLine : $"{singleLine[..maxLength]}...";
    }
}
