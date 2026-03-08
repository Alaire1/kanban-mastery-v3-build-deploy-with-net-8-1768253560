using System.Threading.Tasks;
using KanbanApi.Dtos;

namespace KanbanApi.Services
{
    public interface IUserProfileService
    {
        Task<UserProfileResponseDto> GetUserProfileAsync(string userId);
    }
}
