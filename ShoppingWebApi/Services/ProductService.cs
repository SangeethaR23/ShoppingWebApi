using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Products;
using ShoppingWebApi.Models.DTOs.Reviews;

namespace ShoppingWebApi.Services
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;

        public ProductService(AppDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        // -----------------------------
        // Create
        // -----------------------------
        public async Task<ProductReadDto> CreateAsync(ProductCreateDto dto, CancellationToken ct = default)
        {
            await EnsureCategoryExists(dto.CategoryId, ct);
            await EnsureUniqueSku(dto.SKU, null, ct);

            var entity = new Product
            {
                Name = dto.Name.Trim(),
                SKU = dto.SKU.Trim(),
                Price = dto.Price,
                CategoryId = dto.CategoryId,
                Description = dto.Description?.Trim(),
                IsActive = dto.IsActive
            };

            _db.Products.Add(entity);

            // Ensure 1:1 Inventory row exists on create
            _db.Inventories.Add(new Inventory
            {
                Product = entity,
                Quantity = 0,
                ReorderLevel = 0
            });

            await _db.SaveChangesAsync(ct);

            // Load with navigation for mapping
            var withNavs = await _db.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .FirstAsync(p => p.Id == entity.Id, ct);

            var dtoRead = _mapper.Map<ProductReadDto>(withNavs);
            dtoRead.AverageRating = 0;
            dtoRead.ReviewsCount = 0;
            return dtoRead;
        }

        // -----------------------------
        // Update
        // -----------------------------
        public async Task<ProductReadDto?> UpdateAsync(int id, ProductUpdateDto dto, CancellationToken ct = default)
        {
            if (id != dto.Id)
            {
                throw new BusinessValidationException(
                    "Route id and payload id do not match.",
                    new Dictionary<string, string[]> { ["id"] = new[] { "Mismatch" } });
            }

            var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (entity == null)
                throw new NotFoundException("Product not found.");

            await EnsureCategoryExists(dto.CategoryId, ct);
            await EnsureUniqueSku(dto.SKU, id, ct);

            entity.Name = dto.Name.Trim();
            entity.SKU = dto.SKU.Trim();
            entity.Price = dto.Price;
            entity.Description = dto.Description?.Trim();
            entity.CategoryId = dto.CategoryId;
            entity.IsActive = dto.IsActive;
            entity.UpdatedUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            var withNavs = await _db.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .FirstAsync(p => p.Id == id, ct);

            var res = _mapper.Map<ProductReadDto>(withNavs);

            var rating = await _db.Reviews
                .Where(r => r.ProductId == id)
                .GroupBy(r => r.ProductId)
                .Select(g => new { Avg = g.Average(x => (double)x.Rating), Count = g.Count() })
                .FirstOrDefaultAsync(ct);

            res.AverageRating = rating?.Avg is double a ? Math.Round(a, 2) : 0;
            res.ReviewsCount = rating?.Count ?? 0;

            return res;
        }

        // -----------------------------
        // Delete (guard if referenced by OrderItems)
        // -----------------------------
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (entity == null)
                throw new NotFoundException("Product not found.");

            var usedInOrders = await _db.OrderItems
                .AsNoTracking()
                .AnyAsync(oi => oi.ProductId == id, ct);

            if (usedInOrders)
                throw new ConflictException("Product is referenced by orders and cannot be deleted.");

            _db.Products.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // -----------------------------
        // Get by Id (with rating summary)
        // -----------------------------
        public async Task<ProductReadDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (entity == null)
                throw new NotFoundException("Product not found.");

            var dto = _mapper.Map<ProductReadDto>(entity);

            var rating = await _db.Reviews
                .Where(r => r.ProductId == id)
                .GroupBy(r => r.ProductId)
                .Select(g => new { Avg = g.Average(x => (double)x.Rating), Count = g.Count() })
                .FirstOrDefaultAsync(ct);

            dto.AverageRating = rating?.Avg is double a ? Math.Round(a, 2) : 0;
            dto.ReviewsCount = rating?.Count ?? 0;

            return dto;
        }

        // -----------------------------
        // Get All (paged & sorted) - simpler than Search
        // -----------------------------
        public async Task<PagedResult<ProductReadDto>> GetAllAsync(
            int page,
            int size,
            string? sortBy = "newest",
            string? sortDir = "desc",
            CancellationToken ct = default)
        {
            page = page <= 0 ? 1 : page;
            size = size <= 0 ? 20 : size;

            //var q = _db.Products
            //    .AsNoTracking()
            //    .Include(p => p.Images);

            var sb = (sortBy ?? "newest").ToLowerInvariant();
            var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

            IQueryable<Product> q = _db.Products
                 .AsNoTracking()
                 .Include(p => p.Images);   

            var temp = q;

            q = sb switch
            {
                "price" => desc ? temp.OrderByDescending(p => p.Price) : temp.OrderBy(p => p.Price),
                "name" => desc ? temp.OrderByDescending(p => p.Name) : temp.OrderBy(p => p.Name),
                "newest" => desc ? temp.OrderByDescending(p => p.CreatedUtc) : temp.OrderBy(p => p.CreatedUtc),
                _ => desc ? temp.OrderByDescending(p => p.CreatedUtc) : temp.OrderBy(p => p.CreatedUtc)
            };


            var total = await q.CountAsync(ct);
            var data = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);

            var ids = data.Select(p => p.Id).ToList();
            var ratings = await _db.Reviews
                .Where(r => ids.Contains(r.ProductId))
                .GroupBy(r => r.ProductId)
                .Select(g => new { ProductId = g.Key, Avg = g.Average(x => (double)x.Rating), Count = g.Count() })
                .ToDictionaryAsync(x => x.ProductId, x => (avg: x.Avg, count: x.Count), ct);

            var items = data.Select(p =>
            {
                var dto = _mapper.Map<ProductReadDto>(p);
                if (ratings.TryGetValue(p.Id, out var r))
                {
                    dto.AverageRating = Math.Round(r.avg, 2);
                    dto.ReviewsCount = r.count;
                }
                return dto;
            }).ToList();

            return new PagedResult<ProductReadDto>
            {
                Items = items,
                PageNumber = page,
                PageSize = size,
                TotalCount = total
            };
        }

        // -----------------------------
        // Search with filters (+ rating sorting)
        // -----------------------------
        public async Task<PagedResult<ProductReadDto>> SearchAsync(ProductQuery query, CancellationToken ct = default)
        {
            var page = query.Page <= 0 ? 1 : query.Page;
            var size = query.Size <= 0 ? 20 : query.Size;

            // Build category set (with descendants if requested)
            HashSet<int>? categoryIds = null;
            if (query.CategoryId.HasValue)
            {
                categoryIds = new HashSet<int> { query.CategoryId.Value };
                if (query.IncludeChildren)
                {
                    foreach (var id in await GetDescendantCategoryIds(query.CategoryId.Value, ct))
                        categoryIds.Add(id);
                }
            }

            var baseQuery = _db.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .Where(p => p.IsActive); // only active in catalog

            if (categoryIds != null)
                baseQuery = baseQuery.Where(p => categoryIds.Contains(p.CategoryId));

            if (!string.IsNullOrWhiteSpace(query.NameContains))
            {
                var term = query.NameContains.Trim();
                baseQuery = baseQuery.Where(p => p.Name.Contains(term));
            }

            if (query.PriceMin.HasValue) baseQuery = baseQuery.Where(p => p.Price >= query.PriceMin.Value);
            if (query.PriceMax.HasValue) baseQuery = baseQuery.Where(p => p.Price <= query.PriceMax.Value);

            if (query.InStockOnly == true)
            {
                baseQuery = baseQuery.Where(p =>
                    _db.Inventories.Any(inv => inv.ProductId == p.Id && inv.Quantity > 0));
            }

            if (query.RatingMin.HasValue)
            {
                var min = query.RatingMin.Value;
                baseQuery = baseQuery.Where(p =>
                    _db.Reviews.Where(r => r.ProductId == p.Id).Any() &&
                    _db.Reviews.Where(r => r.ProductId == p.Id).Average(r => (double)r.Rating) >= min);
            }

            var sortBy = (query.SortBy ?? "newest").ToLowerInvariant();
            var desc = string.Equals(query.SortDir, "desc", StringComparison.OrdinalIgnoreCase);

            if (sortBy == "rating")
            {
                var ratingAgg = _db.Reviews
                    .GroupBy(r => r.ProductId)
                    .Select(g => new { ProductId = g.Key, Avg = g.Average(x => (double)x.Rating), Count = g.Count() });

                var q2 = from p in baseQuery
                         join r in ratingAgg on p.Id equals r.ProductId into rj
                         from r in rj.DefaultIfEmpty()
                         select new
                         {
                             Product = p,
                             Avg = r == null ? 0 : r.Avg,
                             Count = r == null ? 0 : r.Count
                         };

                q2 = desc ? q2.OrderByDescending(x => x.Avg).ThenByDescending(x => x.Product.CreatedUtc)
                          : q2.OrderBy(x => x.Avg).ThenBy(x => x.Product.CreatedUtc);

                var total = await q2.CountAsync(ct);
                var rows = await q2.Skip((page - 1) * size).Take(size).ToListAsync(ct);

                var items = new List<ProductReadDto>(rows.Count);
                foreach (var row in rows)
                {
                    var dto = _mapper.Map<ProductReadDto>(row.Product);
                    dto.AverageRating = Math.Round(row.Avg, 2);
                    dto.ReviewsCount = row.Count;
                    items.Add(dto);
                }

                return new PagedResult<ProductReadDto>
                {
                    Items = items,
                    PageNumber = page,
                    PageSize = size,
                    TotalCount = total
                };
            }
            else
            {
                baseQuery = sortBy switch
                {
                    "price" => desc ? baseQuery.OrderByDescending(p => p.Price) : baseQuery.OrderBy(p => p.Price),
                    "name" => desc ? baseQuery.OrderByDescending(p => p.Name) : baseQuery.OrderBy(p => p.Name),
                    "newest" or _ => desc ? baseQuery.OrderByDescending(p => p.CreatedUtc) : baseQuery.OrderBy(p => p.CreatedUtc),
                };

                var total = await baseQuery.CountAsync(ct);
                var data = await baseQuery.Skip((page - 1) * size).Take(size).ToListAsync(ct);

                var ids = data.Select(p => p.Id).ToList();
                var ratings = await _db.Reviews
                    .Where(r => ids.Contains(r.ProductId))
                    .GroupBy(r => r.ProductId)
                    .Select(g => new { ProductId = g.Key, Avg = g.Average(x => (double)x.Rating), Count = g.Count() })
                    .ToDictionaryAsync(x => x.ProductId, x => (avg: x.Avg, count: x.Count), ct);

                var items = data.Select(p =>
                {
                    var dto = _mapper.Map<ProductReadDto>(p);
                    if (ratings.TryGetValue(p.Id, out var r))
                    {
                        dto.AverageRating = Math.Round(r.avg, 2);
                        dto.ReviewsCount = r.count;
                    }
                    return dto;
                }).ToList();

                return new PagedResult<ProductReadDto>
                {
                    Items = items,
                    PageNumber = page,
                    PageSize = size,
                    TotalCount = total
                };
            }
        }

        // -----------------------------
        // Add Image
        // -----------------------------
        public async Task AddImageAsync(int productId, ProductImageCreateDto dto, CancellationToken ct = default)
        {
            var productExists = await _db.Products.AsNoTracking().AnyAsync(p => p.Id == productId, ct);
            if (!productExists)
                throw new NotFoundException("Product not found.");

            var img = new ProductImage { ProductId = productId, Url = dto.Url.Trim() };
            _db.ProductImages.Add(img);
            await _db.SaveChangesAsync(ct);
        }

        // -----------------------------
        // Remove Image
        // -----------------------------
        public async Task<bool> RemoveImageAsync(int productId, int imageId, CancellationToken ct = default)
        {
            var image = await _db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, ct);
            if (image == null)
                throw new NotFoundException("Product image not found.");

            _db.ProductImages.Remove(image);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // -----------------------------
        // Set Active / Inactive
        // -----------------------------
        public async Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
        {
            var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (entity == null)
                throw new NotFoundException("Product not found.");

            entity.IsActive = isActive;
            entity.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        // -----------------------------
        // Get Reviews by ProductId (paged, filter, sort)
        // -----------------------------
        public async Task<PagedResult<ReviewReadDto>> GetReviewsByProductIdAsync(
            int productId,
            int page,
            int size,
            int? minRating = null,
            string? sortBy = "newest",
            string? sortDir = "desc",
            CancellationToken ct = default)
        {
            var exists = await _db.Products.AsNoTracking().AnyAsync(p => p.Id == productId, ct);
            if (!exists)
                throw new NotFoundException("Product not found.");

            page = page <= 0 ? 1 : page;
            size = size <= 0 ? 20 : size;

            // Join Users + UserDetails to build a friendly UserName
            var q = _db.Reviews
                .AsNoTracking()
                .Where(r => r.ProductId == productId)
                .Join(_db.Users, r => r.UserId, u => u.Id, (r, u) => new { r, u })
                .GroupJoin(_db.UserDetails, ru => ru.u.Id, d => d.UserId, (ru, d) => new { ru.r, ru.u, d = d.FirstOrDefault() })
                .Select(x => new ReviewReadDto
                {
                    Id = x.r.Id,
                    ProductId = x.r.ProductId,
                    UserId = x.r.UserId,
                    Rating = x.r.Rating,
                    Comment = x.r.Comment,
                    CreatedUtc = x.r.CreatedUtc,
                    UserName = x.d != null
                        ? (string.IsNullOrWhiteSpace(x.d.FirstName) && string.IsNullOrWhiteSpace(x.d.LastName)
                            ? x.u.Email
                            : $"{x.d.FirstName} {x.d.LastName}".Trim())
                        : x.u.Email
                });

            if (minRating.HasValue)
            {
                q = q.Where(r => r.Rating >= minRating.Value);
            }

            var sb = (sortBy ?? "newest").ToLowerInvariant();
            var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

            q = sb switch
            {
                "rating" => desc ? q.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedUtc)
                                 : q.OrderBy(r => r.Rating).ThenBy(r => r.CreatedUtc),
                "newest" or _ => desc ? q.OrderByDescending(r => r.CreatedUtc)
                                      : q.OrderBy(r => r.CreatedUtc),
            };

            var total = await q.CountAsync(ct);
            var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);

            return new PagedResult<ReviewReadDto>
            {
                Items = items,
                PageNumber = page,
                PageSize = size,
                TotalCount = total
            };
        }

        // ============================================================
        // Helpers
        // ============================================================
        private async Task EnsureCategoryExists(int categoryId, CancellationToken ct)
        {
            var exists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == categoryId, ct);
            if (!exists)
                throw new NotFoundException("Category not found.");
        }

        private async Task EnsureUniqueSku(string sku, int? excludeProductId, CancellationToken ct)
        {
            sku = sku.Trim();
            var exists = await _db.Products.AsNoTracking()
                .AnyAsync(p => p.SKU == sku && (!excludeProductId.HasValue || p.Id != excludeProductId.Value), ct);
            if (exists)
                throw new ConflictException("A product with the same SKU already exists.");
        }

        private async Task<List<int>> GetDescendantCategoryIds(int rootCategoryId, CancellationToken ct)
        {
            // BFS to collect all descendant category IDs
            var result = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(rootCategoryId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                var children = await _db.Categories
                    .AsNoTracking()
                    .Where(c => c.ParentCategoryId == current)
                    .Select(c => c.Id)
                    .ToListAsync(ct);

                foreach (var child in children)
                {
                    result.Add(child);
                    queue.Enqueue(child);
                }
            }

            return result;
        }
    }
}