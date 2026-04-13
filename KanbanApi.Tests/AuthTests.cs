using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KanbanApi.Data;
using KanbanApi;
using KanbanApi.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace KanbanApi.Tests
{
    public class AuthTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AuthTests(WebApplicationFactory<Program> factory)
        {
            var dbName = $"TestDb_Auth_{Guid.NewGuid()}";

            _factory = factory.WithWebHostBuilder(builder =>
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
                        options.UseInMemoryDatabase(dbName));
                });
            });

            _client = _factory.CreateClient();
        }

        private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedUserForCustomEndpoints()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var userName = $"user_{suffix}";
            var email = $"user_{suffix}@example.com";
            const string password = "Password123!";

            var client = _factory.CreateClient();

            var register = await client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password,
                userName
            });
            Assert.Equal(HttpStatusCode.OK, register.StatusCode);

            var login = await client.PostAsJsonAsync("/api/auth/login", new
            {
                identifier = userName,
                password
            });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(auth);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

            var profileResp = await client.GetAsync("/api/users/me");
            Assert.Equal(HttpStatusCode.OK, profileResp.StatusCode);
            var profile = await profileResp.Content.ReadFromJsonAsync<UserProfileResponse>();
            Assert.NotNull(profile);

            return (client, profile!.Id);
        }

        private static MultipartFormDataContent CreateImageFormData(
            byte[] bytes,
            string fileName = "avatar.png",
            string contentType = "image/png")
        {
            var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(fileContent, "file", fileName);
            return form;
        }

        private string MapProfileUrlToAbsolutePath(string relativeUrl)
        {
            var env = _factory.Services.GetRequiredService<IWebHostEnvironment>();
            var root = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            var relativePath = relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(root, relativePath);
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
            TestConsole.FileHeader();
            Console.WriteLine($"\nPrint user data:\nId: {TestConsole.Value(profile.Id, ConsoleColor.Cyan)}\nUserName: {TestConsole.Value(profile.UserName, ConsoleColor.Cyan)}\nEmail: {TestConsole.Value(profile.Email, ConsoleColor.Cyan)}\nDisplayName: {TestConsole.Value(profile.DisplayName ?? string.Empty, ConsoleColor.Cyan)}\n");
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

        [Fact]
        public async Task CustomRegister_WithUniqueUserName_ReturnsOk()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var response = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email = $"custom_{suffix}@example.com",
                password = "Password123!",
                userName = $"custom_user_{suffix}"
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CustomRegister_WithDuplicateUserName_ReturnsConflict()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var userName = $"duplicate_user_{suffix}";

            var first = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email = $"first_{suffix}@example.com",
                password = "Password123!",
                userName
            });
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email = $"second_{suffix}@example.com",
                password = "Password123!",
                userName
            });

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }

        [Fact]
        public async Task CustomLogin_WithUserName_ReturnsOkWithToken()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var userName = $"login_user_{suffix}";
            var email = $"login_{suffix}@example.com";
            const string password = "Password123!";

            var register = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password,
                userName
            });
            Assert.Equal(HttpStatusCode.OK, register.StatusCode);

            var login = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                identifier = userName,
                password
            });

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var responseContent = await login.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(responseContent?.AccessToken);
        }

        [Fact]
        public async Task CustomLogin_WithEmail_ReturnsOkWithToken()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var userName = $"login_user_{suffix}";
            var email = $"login_{suffix}@example.com";
            const string password = "Password123!";

            var register = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password,
                userName
            });
            Assert.Equal(HttpStatusCode.OK, register.StatusCode);

            var login = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                identifier = email,
                password
            });

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var responseContent = await login.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(responseContent?.AccessToken);
        }

        [Fact]
        public async Task CustomLogin_MissingIdentifier_ReturnsBadRequest()
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                identifier = "   ",
                password = "Password123!"
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CustomLogin_MissingPassword_ReturnsBadRequest()
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                identifier = "someone@example.com",
                password = "   "
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CustomLogin_UnknownUser_ReturnsUnauthorized()
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                identifier = $"missing_{Guid.NewGuid():N}@example.com",
                password = "Password123!"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CustomLogin_WrongPassword_ReturnsUnauthorized()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var userName = $"wrong_pass_user_{suffix}";
            var email = $"wrong_pass_{suffix}@example.com";

            var register = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password = "Password123!",
                userName
            });
            Assert.Equal(HttpStatusCode.OK, register.StatusCode);

            var response = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                identifier = userName,
                password = "NotTheRightPassword123!"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CustomRegister_MissingEmail_ReturnsBadRequest()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var response = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email = "   ",
                password = "Password123!",
                userName = $"user_{suffix}"
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CustomRegister_MissingPassword_ReturnsBadRequest()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var response = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email = $"missing_pass_{suffix}@example.com",
                password = "   ",
                userName = $"user_{suffix}"
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CustomRegister_MissingUserName_ReturnsBadRequest()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var response = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email = $"missing_user_{suffix}@example.com",
                password = "Password123!",
                userName = "   "
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CustomRegister_WithDuplicateEmail_ReturnsConflict()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var email = $"dup_email_{suffix}@example.com";

            var first = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password = "Password123!",
                userName = $"first_{suffix}"
            });
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password = "Password123!",
                userName = $"second_{suffix}"
            });

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }

        [Fact]
        public async Task CustomRegister_WeakPassword_ReturnsValidationProblem()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var response = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email = $"weak_{suffix}@example.com",
                password = "123",
                userName = $"weak_user_{suffix}"
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("errors", body, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadProfileImage_WithoutFile_ReturnsBadRequest()
        {
            var (client, _) = await CreateAuthenticatedUserForCustomEndpoints();

            using var form = new MultipartFormDataContent();
            var response = await client.PostAsync("/api/users/me/profile-image", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UploadProfileImage_FileTooLarge_ReturnsBadRequest()
        {
            var (client, _) = await CreateAuthenticatedUserForCustomEndpoints();

            var bytes = new byte[(2 * 1024 * 1024) + 1];
            using var form = CreateImageFormData(bytes, "huge.png", "image/png");
            var response = await client.PostAsync("/api/users/me/profile-image", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UploadProfileImage_NonImageContentType_ReturnsBadRequest()
        {
            var (client, _) = await CreateAuthenticatedUserForCustomEndpoints();

            var bytes = new byte[] { 1, 2, 3, 4 };
            using var form = CreateImageFormData(bytes, "avatar.png", "text/plain");
            var response = await client.PostAsync("/api/users/me/profile-image", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UploadProfileImage_UnsupportedExtension_ReturnsBadRequest()
        {
            var (client, _) = await CreateAuthenticatedUserForCustomEndpoints();

            var bytes = new byte[] { 1, 2, 3, 4 };
            using var form = CreateImageFormData(bytes, "avatar.bmp", "image/png");
            var response = await client.PostAsync("/api/users/me/profile-image", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UploadProfileImage_ValidImage_ReturnsOkAndWritesFile()
        {
            var (client, _) = await CreateAuthenticatedUserForCustomEndpoints();

            var bytes = new byte[] { 137, 80, 78, 71, 1, 2, 3, 4 };
            using var form = CreateImageFormData(bytes, "avatar.png", "image/png");
            var response = await client.PostAsync("/api/users/me/profile-image", form);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var imageUrl = doc.RootElement.GetProperty("imageUrl").GetString();
            Assert.False(string.IsNullOrWhiteSpace(imageUrl));

            var absolutePath = MapProfileUrlToAbsolutePath(imageUrl!);
            Assert.True(File.Exists(absolutePath));

            File.Delete(absolutePath);
        }

        [Fact]
        public async Task UploadProfileImage_SecondUpload_ReplacesPreviousImage()
        {
            var (client, _) = await CreateAuthenticatedUserForCustomEndpoints();

            using var firstForm = CreateImageFormData(new byte[] { 1, 2, 3, 4 }, "first.png", "image/png");
            var firstResp = await client.PostAsync("/api/users/me/profile-image", firstForm);
            Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);
            var firstBody = await firstResp.Content.ReadAsStringAsync();
            var firstUrl = JsonDocument.Parse(firstBody).RootElement.GetProperty("imageUrl").GetString();
            Assert.False(string.IsNullOrWhiteSpace(firstUrl));
            var firstPath = MapProfileUrlToAbsolutePath(firstUrl!);
            Assert.True(File.Exists(firstPath));

            using var secondForm = CreateImageFormData(new byte[] { 5, 6, 7, 8 }, "second.png", "image/png");
            var secondResp = await client.PostAsync("/api/users/me/profile-image", secondForm);
            Assert.Equal(HttpStatusCode.OK, secondResp.StatusCode);
            var secondBody = await secondResp.Content.ReadAsStringAsync();
            var secondUrl = JsonDocument.Parse(secondBody).RootElement.GetProperty("imageUrl").GetString();
            Assert.False(string.IsNullOrWhiteSpace(secondUrl));

            Assert.NotEqual(firstUrl, secondUrl);
            var secondPath = MapProfileUrlToAbsolutePath(secondUrl!);
            Assert.True(File.Exists(secondPath));
            Assert.False(File.Exists(firstPath));

            File.Delete(secondPath);
        }

        [Fact]
        public async Task UploadProfileImage_WhenUserMissing_ReturnsNotFound()
        {
            var (client, userId) = await CreateAuthenticatedUserForCustomEndpoints();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                Assert.NotNull(user);
                db.Users.Remove(user!);
                await db.SaveChangesAsync();
            }

            using var form = CreateImageFormData(new byte[] { 1, 2, 3, 4 }, "avatar.png", "image/png");
            var response = await client.PostAsync("/api/users/me/profile-image", form);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
