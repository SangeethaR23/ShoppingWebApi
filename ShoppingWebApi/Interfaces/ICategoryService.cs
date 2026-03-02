using ShoppingWebApi.Models.DTOs.Categories;
using ShoppingWebApi.Models.DTOs.Common;

namespace ShoppingWebApi.Interfaces
{
    public interface ICategoryService
    {
        Task<CategoryReadDto> CreateAsync(CategoryCreateDto dto, CancellationToken ct = default);
        Task<CategoryReadDto?> UpdateAsync(int id, CategoryUpdateDto dto, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);

        Task<CategoryReadDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<PagedResult<CategoryReadDto>> GetAllAsync(
            int page, int size, string? sortBy = "name", string? sortDir = "asc", CancellationToken ct = default);
    }
}