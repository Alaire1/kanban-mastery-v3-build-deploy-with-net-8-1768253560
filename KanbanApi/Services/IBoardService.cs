using System.Collections.Generic;
using System.Threading.Tasks;
using KanbanApi.Models;
using KanbanApi.Dtos;

namespace KanbanApi.Services
{
    public interface IBoardService
    {
        Task<BoardDto> CreateBoardAsync(string name, string ownerId);
        Task<BoardResult> GetBoardAsync(string userId, int boardId);
        Task<Board?> GetBoardByIdAsync(int id);
        Task<IEnumerable<Board>> GetAllBoardsAsync();
        Task UpdateBoardAsync(Board board);
        Task DeleteBoardAsync(int id);
        Task<IEnumerable<Board>> GetBoardsByUserIdAsync(int userId);
    }
}