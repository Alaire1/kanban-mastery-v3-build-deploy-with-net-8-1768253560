using System.Security.Claims;
using KanbanApi.Dtos;
using KanbanApi.Models;
using Microsoft.AspNetCore.Identity;

namespace KanbanApi.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users").RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal user, UserManager<ApplicationUser> userManager) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var appUser = await userManager.FindByIdAsync(userId);
            if (appUser is null)
                return Results.NotFound();

            var dto = new UserProfileResponseDto
            {
                Id = appUser.Id,
                UserName = appUser.UserName ?? string.Empty,
                Email = appUser.Email,
                DisplayName = appUser.DisplayName ?? string.Empty 

            };

            return Results.Ok(dto);
        });

        return routes;
    }
}
