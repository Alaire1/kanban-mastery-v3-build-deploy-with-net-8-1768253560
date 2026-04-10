using System.Security.Claims;
using KanbanApi.Dtos;
using KanbanApi.Models;
using Microsoft.AspNetCore.Identity;
using KanbanApi.Services;
using System.IO;

namespace KanbanApi.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/auth/login", async Task<IResult> (
            LoginDto request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager) =>
        {
            var identifier = (request.Identifier ?? request.Email)?.Trim();
            var password = request.Password;

            if (string.IsNullOrWhiteSpace(identifier))
                return Results.BadRequest(new { message = "Email or UserName is required." });

            if (string.IsNullOrWhiteSpace(password))
                return Results.BadRequest(new { message = "Password is required." });

            ApplicationUser? user = identifier.Contains('@')
                ? await userManager.FindByEmailAsync(identifier)
                : await userManager.FindByNameAsync(identifier);

            user ??= await userManager.FindByNameAsync(identifier);

            if (user is null)
                return Results.Unauthorized();

            var isValid = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
            if (!isValid.Succeeded)
                return Results.Unauthorized();

            var principal = await signInManager.CreateUserPrincipalAsync(user);
            return Results.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
        });

        routes.MapPost("/api/auth/register", async Task<IResult> (
            RegisterDto request,
            UserManager<ApplicationUser> userManager) =>
        {
            var email = request.Email?.Trim();
            var password = request.Password;
            var userName = request.UserName?.Trim();
            var displayName = request.DisplayName?.Trim();

            if (string.IsNullOrWhiteSpace(email))
                return Results.BadRequest(new { message = "Email is required." });

            if (string.IsNullOrWhiteSpace(password))
                return Results.BadRequest(new { message = "Password is required." });

            if (string.IsNullOrWhiteSpace(userName))
                return Results.BadRequest(new { message = "UserName is required." });

            var existingUserByName = await userManager.FindByNameAsync(userName);
            if (existingUserByName is not null)
                return Results.Conflict(new { message = "This username is already taken." });

            var existingUserByEmail = await userManager.FindByEmailAsync(email);
            if (existingUserByEmail is not null)
                return Results.Conflict(new { message = "This email is already registered." });

            var user = new ApplicationUser
            {
                Email = email,
                UserName = userName,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

                return Results.ValidationProblem(errors);
            }

            return Results.Ok(new { message = "Registration successful." });
        });

        var group = routes.MapGroup("/api/users").RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal user, IUserProfileService userProfileService) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var dto = await userProfileService.GetUserProfileAsync(userId);
            if (dto is null)
                return Results.NotFound();

            return Results.Ok(dto);
        });

        group.MapPost("/me/profile-image", async Task<IResult> (
            ClaimsPrincipal user,
            IFormFile file,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            HttpContext httpContext) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            if (file is null || file.Length == 0)
                return Results.BadRequest(new { message = "Image file is required." });

            const long maxBytes = 2 * 1024 * 1024;
            if (file.Length > maxBytes)
                return Results.BadRequest(new { message = "Image must be 2 MB or smaller." });

            if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { message = "Only image files are allowed." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
                return Results.BadRequest(new { message = "Supported formats: jpg, jpeg, png, gif, webp." });

            var appUser = await userManager.FindByIdAsync(userId);
            if (appUser is null)
                return Results.NotFound(new { message = "User not found." });

            var uploadsRoot = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "profiles");
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{userId}_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            await using (var stream = File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            if (!string.IsNullOrWhiteSpace(appUser.ProfileImageUrl))
            {
                var previousPath = appUser.ProfileImageUrl;
                var marker = "/uploads/profiles/";
                var markerIndex = previousPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex >= 0)
                {
                    var previousRelative = previousPath[(markerIndex + 1)..].Replace('/', Path.DirectorySeparatorChar);
                    var previousAbsolute = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), previousRelative);
                    if (File.Exists(previousAbsolute))
                        File.Delete(previousAbsolute);
                }
            }

            var relativeUrl = $"/uploads/profiles/{fileName}";
            appUser.ProfileImageUrl = relativeUrl;

            var updateResult = await userManager.UpdateAsync(appUser);
            if (!updateResult.Succeeded)
                return Results.StatusCode(StatusCodes.Status500InternalServerError);

            return Results.Ok(new { imageUrl = relativeUrl });
        })
        .DisableAntiforgery();

        return routes;
    }
}
