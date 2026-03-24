using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;
using KanbanApi.Dtos;


namespace KanbanApi.Services
{
    public class BoardService : IBoardService
    {
        private readonly ApplicationDbContext _context;

        public BoardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<BoardDto> CreateBoardAsync(string name, string ownerId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Board name cannot be null or empty.", nameof(name));

            var board = new Board(name, ownerId); // constructor adds owner membership itself
            _context.Boards.Add(board);
            await _context.SaveChangesAsync();

            // Ensure the board owner is also recorded as a BoardMember with Owner role
            var alreadyMember = await _context.BoardMembers
                .AnyAsync(m => m.BoardId == board.Id && m.UserId == ownerId);

            if (!alreadyMember)
            {
                var ownerMember = new BoardMember(ownerId, board.Id, "Owner");
                _context.BoardMembers.Add(ownerMember);
                // Also add to the in-memory navigation property so the returned board includes the member
                board.Members.Add(ownerMember);
                await _context.SaveChangesAsync();
            }

            // Ensure in-memory navigation collection has unique members (defensive)
            var distinctMembers = board.Members
                .GroupBy(m => m.UserId)
                .Select(g => g.First())
                .ToList();

            board.Members.Clear();
            foreach (var m in distinctMembers)
                board.Members.Add(m);

            return new BoardDto
            {
                Id = board.Id,
                Name = board.Name,
                OwnerId = board.OwnerId,
                Role = "Owner",
                Members = board.Members.Select(m => m.UserId).ToList()
            };
        }

        public async Task<BoardResult> GetBoardAsync(string userId, int boardId)
        {
        var board = await _context.Boards
            .AsNoTracking()
            .Include(b => b.Columns).ThenInclude(c => c.Cards)
            .Include(b => b.Members)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null)
        return new BoardResult.NotFound("Board not found.");

        var isMember = board.OwnerId == userId || board.Members.Any(m => m.UserId == userId);
        if (!isMember)
            return new BoardResult.Forbidden();

        var membership = board.Members.FirstOrDefault(m => m.UserId == userId);

        return new BoardResult.Found(new BoardIdResultDto
        {
            Id = board.Id,
            Name = board.Name,
            OwnerId = board.OwnerId,
            Role = board.OwnerId == userId ? "Owner" : membership?.Role ?? "Member",
            Members = board.Members.Select(m => new BoardMemberResultDto
        {
            UserId = m.UserId,
            Role = m.Role
            }).ToList(),
            Columns = board.Columns.OrderBy(c => c.Position).Select(c => new BoardColumnResultDto
            {
                Id = c.Id,
                Name = c.Name,
                Position = c.Position,
                Cards = c.Cards.Select(card => new BoardCardResultDto
                {
                    Id = card.Id,
                    Title = card.Title
                }).ToList()
            }).ToList()
        });
    }

        public async Task<IEnumerable<Board>> GetAllBoardsAsync()
        {
            return await _context.Boards
                .Include(b => b.Owner)
                .ToListAsync();
        }

        public async Task<Board?> GetBoardByIdAsync(int id)
        {
            var board = await _context.Boards.FindAsync(id);
            return board;
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
            string userIdStr = userId.ToString();
            return await _context.Boards
                .Where(b => b.Members.Any(m => m.UserId == userIdStr))
                .ToListAsync();
        }


        public async Task<BoardResult> UpdateBoardAsync(string userId, int boardId, string name)
        {
            var board = await _context.Boards
                .Include(b => b.Members)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board is null)
                return new BoardResult.NotFound("Board not found.");

            if (board.OwnerId != userId)
                return new BoardResult.Forbidden();

            board.Name = name;
            await _context.SaveChangesAsync();

            return new BoardResult.Updated(new BoardIdResultDto
            {
                Id = board.Id,
                Name = board.Name,
                OwnerId = board.OwnerId,
                Role = "Owner",
                Members = board.Members.Select(m => new BoardMemberResultDto
            {
                UserId = m.UserId,
                Role = m.Role
                }).ToList(),
                Columns = new()
            });
        }

        public async Task<BoardResult> DeleteBoardAsync(string userId, int boardId)
        {
            var board = await _context.Boards
                .Include(b => b.Columns)
                .ThenInclude(c => c.Cards)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board is null)
                return new BoardResult.NotFound("Board not found.");

            if (board.OwnerId != userId)
                return new BoardResult.Forbidden();

            _context.Boards.Remove(board);
            await _context.SaveChangesAsync();

            return new BoardResult.Deleted();
        }

    }
}