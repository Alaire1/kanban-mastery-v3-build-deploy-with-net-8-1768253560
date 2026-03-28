using System.Security.Claims;
using KanbanApi.Dtos;
using KanbanApi.Filters;
using KanbanApi.Services;

namespace KanbanApi.Endpoints;

public static class CardsAssignEndpoint
{
    public static IEndpointRouteBuilder MapCardsAssignEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/boards/{boardId}/cards/{cardId}/assign").RequireAuthorization();

        group.MapPut("/", async Task<IResult> (
            int boardId,
            int cardId,
            AssignCardRequestDto dto,
            ClaimsPrincipal user,
            ICardService cardService,
            IBoardService boardService) =>
        {
            var result = await cardService.AssignCardAsync(cardId, dto.UserId, boardId);
            return result.ResultType switch
            {
                CardResultType.Success        => TypedResults.Ok(result.AssignDto),
                CardResultType.CardNotFound   => TypedResults.NotFound("Card not found."),
                CardResultType.ColumnNotFound => TypedResults.NotFound("Column not found."),
                CardResultType.Forbidden      => TypedResults.Forbid(),
                CardResultType.BadRequest     => TypedResults.BadRequest(result.Message ?? "Bad request."),
                _                             => TypedResults.StatusCode(500)
            };
        })
        .WithValidation<AssignCardRequestDto>()
        .RequireAuthorization("IsBoardMember");

        return routes;
    }
}