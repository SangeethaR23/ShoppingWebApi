using System.Threading;
using System.Threading.Tasks;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Inventory;

namespace ShoppingWebApi.Interfaces
{
    public interface IInventoryService
    {
        Task<InventoryReadDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<InventoryReadDto?> GetByProductIdAsync(int productId, CancellationToken ct = default);

        Task<PagedResult<InventoryReadDto>> GetPagedAsync(
            int? productId = null, int? categoryId = null, string? sku = null,
            bool? lowStockOnly = null, string? sortBy = "product", bool desc = false,
            int page = 1, int size = 10, CancellationToken ct = default);

        Task<InventoryReadDto> AdjustAsync(int productId, int delta, string? reason = null, CancellationToken ct = default);
        Task<InventoryReadDto> SetQuantityAsync(int productId, int quantity, CancellationToken ct = default);
        Task<InventoryReadDto> SetReorderLevelAsync(int productId, int reorderLevel, CancellationToken ct = default);
    }
}