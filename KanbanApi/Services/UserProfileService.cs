using System;
using System.Threading.Tasks;
using KanbanApi.Dtos;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;
using KanbanApi.Data;

namespace KanbanApi.Services
{

    public class UserProfileService : IUserProfileService
    {
        private readonly ApplicationDbContext _context;

        public UserProfileService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UserProfileResponseDto> GetUserProfileAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found.");

            return new UserProfileResponseDto
            {
                Id = user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                DisplayName = user.DisplayName // if exists
            };
        }
    }
}
