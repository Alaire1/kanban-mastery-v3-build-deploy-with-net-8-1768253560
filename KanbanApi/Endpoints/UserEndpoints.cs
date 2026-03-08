using System.Security.Claims;
using KanbanApi.Dtos;
using KanbanApi.Models;
using Microsoft.AspNetCore.Identity;
using KanbanApi.Services;

namespace KanbanApi.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
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

        return routes;
    }
}
