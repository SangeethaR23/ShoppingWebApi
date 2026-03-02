using System.Threading;
using System.Threading.Tasks;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Users;

namespace ShoppingWebApi.Interfaces
{
    public interface IUserService
    {
        // Admin
        Task<PagedResult<UserListItemDto>> GetPagedAsync(string? email, string? role, string? name, string? sortBy, bool desc, int page, int size, CancellationToken ct = default);
        Task<UserProfileReadDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<bool> UpdateRoleAsync(int id, string role, CancellationToken ct = default);

        // User
        Task<UserProfileReadDto?> GetProfileAsync(int userId, CancellationToken ct = default);
        Task<UserProfileReadDto> UpdateProfileAsync(int userId, UpdateUserProfileDto dto, CancellationToken ct = default);
        Task<bool> ChangePasswordAsync(ChangePasswordRequestDto dto, CancellationToken ct = default);
    }
}