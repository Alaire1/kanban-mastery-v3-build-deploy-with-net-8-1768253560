using System.Collections.Generic;
using System.Threading.Tasks;
using KanbanApi.Models;

namespace KanbanApi.Services
{
    public interface IBoardService
    {
        Task<Board> CreateBoardAsync(string name);
        Task<Board?> GetBoardByIdAsync(int id);
        Task<IEnumerable<Board>> GetAllBoardsAsync();
        Task UpdateBoardAsync(Board board);
        Task DeleteBoardAsync(int id);
        Task<IEnumerable<Board>> GetBoardsByUserIdAsync(int userId);
    }
}