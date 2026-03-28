using KanbanApi.Dtos;
using KanbanApi.Models;
using KanbanApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

public class CardService : ICardService
{
    private readonly ApplicationDbContext _db;

    public CardService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CardResult> CreateCardAsync(string userId, int boardId, CreateCardRequestDto dto)
    {
        var column = await _db.Columns.FirstOrDefaultAsync(c => c.Id == dto.ColumnId && c.BoardId == boardId);
        if (column == null)
            return CardResult.ColumnNotFound();

        var card = new Card(dto.Title, column)
        {
            Description = dto.Description
        };

        _db.Cards.Add(card);
        await _db.SaveChangesAsync();

        var cardDto = new CardResponseDto
        {
            Id = card.Id,
            Title = card.Title,
            Description = card.Description,
            ColumnId = card.ColumnId
        };

        return CardResult.Success(cardDto);
    }

    public async Task<CardResult> UpdateCardAsync(int cardId, UpdateCardDto dto)
    {
        var card = await _db.Cards.FindAsync(cardId);
        if (card == null)
            return CardResult.CardNotFound();

        card.Title = dto.Title;
        card.Description = dto.Description;
        card.ColumnId = dto.ColumnId;

        await _db.SaveChangesAsync();

        var cardDto = new CardResponseDto
        {
            Id = card.Id,
            Title = card.Title,
            Description = card.Description,
            ColumnId = card.ColumnId
        };

        return CardResult.Success(cardDto);
    }

    public async Task<CardResult> DeleteCardAsync(int cardId, string userId, int boardId)
    {
        var card = await _db.Cards.FindAsync(cardId);
        if (card == null)
            return CardResult.CardNotFound();

        _db.Cards.Remove(card);
        await _db.SaveChangesAsync();

        return CardResult.Success();
    }

    public async Task<Card?> GetCardAsync(int cardId)
    {
        return await _db.Cards.FindAsync(cardId);
    }

    public async Task<CardResult> AssignCardAsync(int cardId, string targetUserId, int boardId)
    {
        // Check if the user being assigned is a board member
        bool isMember = await _db.BoardMembers.AnyAsync(m => m.BoardId == boardId && m.UserId == targetUserId);
        if (!isMember)
            return CardResult.BadRequest("Assignee must be a board member.");

        var card = await _db.Cards.FindAsync(cardId);
        if (card == null)
            return CardResult.CardNotFound();

        card.AssignedUserId = targetUserId;
        await _db.SaveChangesAsync();

        var assignDto = new AssignCardResponseDto
        {
            CardId = card.Id,
            UserId = targetUserId
        };
        return CardResult.Success(assignDto);
    }
}