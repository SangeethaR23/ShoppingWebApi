using System.Threading;
using System.Threading.Tasks;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Reviews;

namespace ShoppingWebApi.Interfaces
{
    public interface IReviewService
    {
        Task<ReviewReadDto> CreateAsync(ReviewCreateDto dto, CancellationToken ct = default);
        Task<ReviewReadDto?> GetAsync(int productId, int userId, CancellationToken ct = default);
        Task<PagedResult<ReviewReadDto>> GetByProductAsync(int productId, int page = 1, int size = 10, CancellationToken ct = default);
        Task<bool> UpdateAsync(int productId, int userId, ReviewUpdateDto dto, CancellationToken ct = default);
        Task<bool> DeleteAsync(int productId, int userId, CancellationToken ct = default);
    }
}