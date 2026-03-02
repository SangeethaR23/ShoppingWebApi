using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Inventory;

namespace ShoppingWebApi.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(AppDbContext db, ILogger<InventoryService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<InventoryReadDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var inv = await _db.Inventories
                .AsNoTracking()
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.Id == id, ct);

            return inv == null ? null : ToDto(inv);
        }

        public async Task<InventoryReadDto?> GetByProductIdAsync(int productId, CancellationToken ct = default)
        {
            var inv = await _db.Inventories
                .AsNoTracking()
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.ProductId == productId, ct);

            return inv == null ? null : ToDto(inv);
        }

        public async Task<PagedResult<InventoryReadDto>> GetPagedAsync(
            int? productId = null, int? categoryId = null, string? sku = null,
            bool? lowStockOnly = null, string? sortBy = "product", bool desc = false,
            int page = 1, int size = 10, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (size < 1) size = 10;

            var q = _db.Inventories.AsNoTracking()
                .Include(i => i.Product)
                .AsQueryable();

            if (productId.HasValue)
                q = q.Where(i => i.ProductId == productId.Value);

            if (categoryId.HasValue)
                q = q.Where(i => i.Product.CategoryId == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(sku))
                q = q.Where(i => i.Product.SKU.Contains(sku));

            if (lowStockOnly == true)
                q = q.Where(i => i.Quantity <= i.ReorderLevel);

            // Sorting
            sortBy = (sortBy ?? "product").ToLowerInvariant();
            q = sortBy switch
            {
                "quantity" => desc ? q.OrderByDescending(i => i.Quantity) : q.OrderBy(i => i.Quantity),
                "reorderlevel" => desc ? q.OrderByDescending(i => i.ReorderLevel) : q.OrderBy(i => i.ReorderLevel),
                _ => desc ? q.OrderByDescending(i => i.Product.Name) : q.OrderBy(i => i.Product.Name)
            };

            var total = await q.CountAsync(ct);
            var rows = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);

            var items = rows.Select(ToDto).ToList();

            return new PagedResult<InventoryReadDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = size
            };
        }

        public async Task<InventoryReadDto> AdjustAsync(int productId, int delta, string? reason = null, CancellationToken ct = default)
        {
            var inv = await _db.Inventories.Include(i => i.Product).FirstOrDefaultAsync(i => i.ProductId == productId, ct);
            if (inv == null)
                throw new NotFoundException($"Inventory not found for product {productId}.");

            var newQty = inv.Quantity + delta;
            if (newQty < 0)
                throw new BusinessValidationException($"Insufficient quantity. Current: {inv.Quantity}, delta: {delta}.");

            inv.Quantity = newQty;
            inv.UpdatedUtc = System.DateTime.UtcNow;

            // NOTE: if you maintain an audit trail, write a LogEntry here with 'reason'
            await _db.SaveChangesAsync(ct);

            return ToDto(inv);
        }

        public async Task<InventoryReadDto> SetQuantityAsync(int productId, int quantity, CancellationToken ct = default)
        {
            if (quantity < 0)
                throw new BusinessValidationException("Quantity cannot be negative.");

            var inv = await _db.Inventories.Include(i => i.Product).FirstOrDefaultAsync(i => i.ProductId == productId, ct);
            if (inv == null)
                throw new NotFoundException($"Inventory not found for product {productId}.");

            inv.Quantity = quantity;
            inv.UpdatedUtc = System.DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return ToDto(inv);
        }

        public async Task<InventoryReadDto> SetReorderLevelAsync(int productId, int reorderLevel, CancellationToken ct = default)
        {
            if (reorderLevel < 0)
                throw new BusinessValidationException("Reorder level cannot be negative.");

            var inv = await _db.Inventories.Include(i => i.Product).FirstOrDefaultAsync(i => i.ProductId == productId, ct);
            if (inv == null)
                throw new NotFoundException($"Inventory not found for product {productId}.");

            inv.ReorderLevel = reorderLevel;
            inv.UpdatedUtc = System.DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return ToDto(inv);
        }

        private static InventoryReadDto ToDto(Inventory inv) => new InventoryReadDto
        {
            Id = inv.Id,
            ProductId = inv.ProductId,
            ProductName = inv.Product?.Name ?? string.Empty,
            SKU = inv.Product?.SKU ?? string.Empty,
            Quantity = inv.Quantity,
            ReorderLevel = inv.ReorderLevel,
            CreatedUtc = inv.CreatedUtc,
            UpdatedUtc = inv.UpdatedUtc
        };
    }
}