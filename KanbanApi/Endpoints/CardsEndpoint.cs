using System.Security.Claims;
using KanbanApi.Dtos;

using KanbanApi.Filters;
using KanbanApi.Services;

namespace KanbanApi.Endpoints;

public static class CardsEndpoint
{
    public static IEndpointRouteBuilder MapCardsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/boards/{boardId}/cards").RequireAuthorization();

        group.MapPost("/", async Task<IResult> (
            int boardId,
            CreateCardRequestDto dto,
            ClaimsPrincipal user,
            ICardService cardService) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            var result = await cardService.CreateCardAsync(userId, boardId, dto);
            return result.ResultType switch
            {
                CardResultType.Success        => TypedResults.Created($"/api/boards/{boardId}/cards/{result.Dto!.Id}", result.Dto),
                CardResultType.ColumnNotFound => TypedResults.NotFound("Column not found."),
                CardResultType.Forbidden      => TypedResults.Forbid(),
                _                             => TypedResults.StatusCode(500)
            };
        })
        .WithValidation<CreateCardRequestDto>()
        .RequireAuthorization("IsBoardMember");

        group.MapPut("/{cardId}", async Task<IResult> (
            int boardId,
            int cardId,
            UpdateCardDto dto,
            ClaimsPrincipal user,
            ICardService cardService) =>
        {
            var result = await cardService.UpdateCardAsync(cardId, dto);
            return result.ResultType switch
            {
                CardResultType.Success       => TypedResults.Ok(result.Dto),
                CardResultType.CardNotFound  => TypedResults.NotFound("Card not found."),
                CardResultType.Forbidden     => TypedResults.Forbid(),
                _                            => TypedResults.StatusCode(500)
            };
        })
        .RequireAuthorization("IsBoardMember")
        .WithValidation<UpdateCardDto>();

        group.MapDelete("/{cardId}", async Task<IResult> (
            int boardId,
            int cardId,
            ClaimsPrincipal user,
            ICardService cardService) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return TypedResults.Unauthorized();

            var result = await cardService.DeleteCardAsync(cardId, userId, boardId);

            return result.ResultType switch
            {
                CardResultType.Success      => TypedResults.NoContent(),
                CardResultType.CardNotFound => TypedResults.NotFound("Card not found."),
                CardResultType.Forbidden    => TypedResults.Forbid(),
                _                           => TypedResults.StatusCode(500)
            };
        })
        .RequireAuthorization("IsBoardMember");
                return routes;
    }
}
