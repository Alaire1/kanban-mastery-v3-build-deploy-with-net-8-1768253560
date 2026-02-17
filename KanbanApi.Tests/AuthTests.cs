using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KanbanApi.Data;
using KanbanApi;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KanbanApi.Tests
{
    public class AuthTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public AuthTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace real DbContext with in-memory for tests
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase("TestDb"));
                });
            }).CreateClient();
        }

        [Fact]
        public async Task GetCurrentUserProfile_WithoutToken_ReturnsUnauthorized()
        {
            // Act: Call /api/users/me without an authorization header
            var response = await _client.GetAsync("/api/users/me");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Register_WithValidData_ReturnsOk()
        {
            var response = await _client.PostAsJsonAsync("/register", new
            {
                email = "test@example.com",
                password = "Test123!"
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsOkWithAuthToken()
        {
            // Arrange: Register a user first
            var registerResponse = await _client.PostAsJsonAsync("/register", new
            {
                email = "testuser@example.com",
                password = "Password123!"
            });
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Act: Login with the same credentials
            var loginResponse = await _client.PostAsJsonAsync("/login", new
            {
                email = "testuser@example.com",
                password = "Password123!"
            });

            // Assert
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var responseContent = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(responseContent?.AccessToken); // Assert the access token is present
        }

        [Fact]
        public async Task GetCurrentUserProfile_WithValidToken_ReturnsUserProfile()
        {
            // Arrange: Register and log in a user
            const string email = "profileuser@example.com";
            const string password = "Password123!";

            var registerResponse = await _client.PostAsJsonAsync("/register", new
            {
                email,
                password
            });
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            var loginResponse = await _client.PostAsJsonAsync("/login", new
            {
                email,
                password
            });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(auth);
            Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));

            // Act: Call /api/users/me with the bearer token
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

            var meResponse = await _client.GetAsync("/api/users/me");

            // Assert
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
            var profile = await meResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
            Assert.NotNull(profile);
            Assert.False(string.IsNullOrWhiteSpace(profile!.Id));
            Assert.Equal(email, profile.Email);
            Assert.False(string.IsNullOrWhiteSpace(profile.UserName));
            
            // Assert.Equal(email, profile.UserName);
            // Print profile properties for visibility in test output
            Console.WriteLine($"\nPrint user data:\nId: {profile.Id}\nUserName: {profile.UserName}\nEmail: {profile.Email}\nDisplayName: {profile.DisplayName}\n");
        }

        [Fact]
        public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
        {
            // Arrange: Register a user first
            var registerResponse1 = await _client.PostAsJsonAsync("/register", new
            {
                email = "duplicate@example.com",
                password = "Password123!"
            });
            Assert.Equal(HttpStatusCode.OK, registerResponse1.StatusCode);

            // Act: Try to register with the same email again
            var registerResponse2 = await _client.PostAsJsonAsync("/register", new
            {
                email = "duplicate@example.com",
                password = "Password123!"
            });

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, registerResponse2.StatusCode);
        }
    }
    // Represents the structure of the authentication response returned by the API.
// This is used in tests to deserialize the JSON response and validate its content.
    public class AuthResponse
    {
        public string TokenType { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class UserProfileResponse
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
    }
}
