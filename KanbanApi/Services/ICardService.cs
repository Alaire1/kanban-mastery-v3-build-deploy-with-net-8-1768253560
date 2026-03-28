using KanbanApi.Dtos;
using KanbanApi.Models;

namespace KanbanApi.Services;

public interface ICardService
{
    Task<CardResult> CreateCardAsync(string userId, int boardId, CreateCardRequestDto dto);
    Task<CardResult> UpdateCardAsync(int cardId, UpdateCardDto dto);
    Task<CardResult> DeleteCardAsync(int cardId, string userId, int boardId);
    Task<Card?> GetCardAsync(int cardId);
    Task<CardResult> AssignCardAsync(int cardId, string targetUserId, int boardId);
}