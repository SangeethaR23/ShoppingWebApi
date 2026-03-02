using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Products;
using ShoppingWebApi.Models.DTOs.Reviews;

namespace ShoppingWebApi.Interfaces
{
    public interface IProductService
    {
        // existing
        Task<ProductReadDto> CreateAsync(ProductCreateDto dto, CancellationToken ct = default);
        Task<ProductReadDto?> UpdateAsync(int id, ProductUpdateDto dto, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
        Task<ProductReadDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<PagedResult<ProductReadDto>> SearchAsync(ProductQuery query, CancellationToken ct = default);
        Task AddImageAsync(int productId, ProductImageCreateDto dto, CancellationToken ct = default);
        Task<bool> RemoveImageAsync(int productId, int imageId, CancellationToken ct = default);
        Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default);

        // NEW
        Task<PagedResult<ProductReadDto>> GetAllAsync(int page, int size, string? sortBy = "newest", string? sortDir = "desc", CancellationToken ct = default);

        Task<PagedResult<ReviewReadDto>> GetReviewsByProductIdAsync(
            int productId,
            int page,
            int size,
            int? minRating = null,        // optional filter
            string? sortBy = "newest",    // newest|rating
            string? sortDir = "desc",
            CancellationToken ct = default);
    }
}