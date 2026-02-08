using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services
{
    public class BoardService : IBoardService
    {
        private readonly ApplicationDbContext _context;

        public BoardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Board> CreateBoardAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Board name cannot be null or empty.", nameof(name));
            }

            var board = new Board(name);
            _context.Boards.Add(board);
            await _context.SaveChangesAsync();
            return board;
        }

        public async Task<Board?> GetBoardByIdAsync(int id)
        {
            return await _context.Boards.FindAsync(id);
        }

        public async Task<IEnumerable<Board>> GetAllBoardsAsync()
        {
            return await _context.Boards.ToListAsync();
        }

        public async Task UpdateBoardAsync(Board board)
        {
            _context.Boards.Update(board);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteBoardAsync(int id)
        {
            var board = await _context.Boards.FindAsync(id);
            if (board != null)
            {
                _context.Boards.Remove(board);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Board>> GetBoardsByUserIdAsync(int userId)
        {
            return await _context.Boards
                .Where(b => b.Members.Any(m => m.UserId == userId.ToString()))
                .ToListAsync();
        }
    }
}