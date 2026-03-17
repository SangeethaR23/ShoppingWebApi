using AutoMapper;
using Microsoft.EntityFrameworkCore;
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
        private readonly IRepository<int, Product> _productRepo;
        private readonly IRepository<int, Category> _categoryRepo;
        private readonly IRepository<int, ProductImage> _imageRepo;
        private readonly IRepository<int, Inventory> _inventoryRepo;

        private readonly IMapper _mapper;

        public ProductService(
            IRepository<int, Product> productRepo,
            IRepository<int, Category> categoryRepo,
            IRepository<int, ProductImage> imageRepo,
            IRepository<int, Inventory> inventoryRepo,
            IMapper mapper)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _imageRepo = imageRepo;
            _inventoryRepo = inventoryRepo;
            _mapper = mapper;
        }

        // ============================================================
        // CREATE (REPO)
        // ============================================================
        public async Task<ProductReadDto> CreateAsync(ProductCreateDto dto, CancellationToken ct = default)
        {
            var category = await _categoryRepo.Get(dto.CategoryId);
            if (category is null)
                throw new NotFoundException("Category not found.");

            var all = await _productRepo.GetAll() ?? Enumerable.Empty<Product>();
            if (all.Any(p => p.SKU == dto.SKU))
                throw new ConflictException("SKU already exists.");

            var entity = new Product
            {
                Name = dto.Name.Trim(),
                SKU = dto.SKU.Trim(),
                Price = dto.Price,
                Description = dto.Description?.Trim(),
                CategoryId = dto.CategoryId,
                IsActive = dto.IsActive
            };

            var added = await _productRepo.Add(entity);
            if (added is null)
                throw new BusinessValidationException("Failed to create product.");

            // Create 1:1 Inventory
            await _inventoryRepo.Add(new Inventory
            {
                ProductId = added.Id,
                Quantity = 0,
                ReorderLevel = 0
            });

            // Return with images (none yet)
            var dtoOut = _mapper.Map<ProductReadDto>(added);
            dtoOut.AverageRating = 0;
            dtoOut.ReviewsCount = 0;

            return dtoOut;
        }

        // ============================================================
        // UPDATE (REPO)
        // ============================================================
        public async Task<ProductReadDto?> UpdateAsync(int id, ProductUpdateDto dto, CancellationToken ct = default)
        {
            if (id != dto.Id)
                throw new BusinessValidationException("Route id mismatch.");

            var entity = await _productRepo.Get(id);
            if (entity is null)
                throw new NotFoundException("Product not found.");

            var category = await _categoryRepo.Get(dto.CategoryId);
            if (category is null)
                throw new NotFoundException("Category not found.");

            var all = await _productRepo.GetAll() ?? Enumerable.Empty<Product>();
            if (all.Any(p => p.SKU == dto.SKU && p.Id != id))
                throw new ConflictException("SKU already exists.");

            entity.Name = dto.Name.Trim();
            entity.SKU = dto.SKU.Trim();
            entity.Price = dto.Price;
            entity.Description = dto.Description?.Trim();
            entity.CategoryId = dto.CategoryId;
            entity.IsActive = dto.IsActive;
            entity.UpdatedUtc = DateTime.UtcNow;

            var updated = await _productRepo.Update(id, entity);
            if (updated is null)
                throw new NotFoundException("Product update failed.");

            var dtoOut = _mapper.Map<ProductReadDto>(updated);

            // Rating calculation - IQueryable version
            var ratings = _productRepo.GetQueryable()
                .Join(_productRepo.GetQueryable(), p => p.Id, p2 => p2.Id, (p, p2) => p)
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    Avg = _productRepo.GetQueryable()
                        .Where(x => x.Id == p.Id)
                        .SelectMany(x => x.Reviews)
                        .Average(r => (double?)r.Rating) ?? 0,

                    Count = _productRepo.GetQueryable()
                        .Where(x => x.Id == p.Id)
                        .SelectMany(x => x.Reviews)
                        .Count()
                })
                .FirstOrDefault();

            dtoOut.AverageRating = Math.Round(ratings?.Avg ?? 0, 2);
            dtoOut.ReviewsCount = ratings?.Count ?? 0;

            return dtoOut;
        }

        // ============================================================
        // DELETE (REPO)
        // ============================================================
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var entity = await _productRepo.Get(id);
            if (entity is null)
                throw new NotFoundException("Product not found.");

            // Check order usage using IQueryable join
            var used = _productRepo.GetQueryable()
                .Where(p => p.Id == id)
                .SelectMany(p => p.OrderItems)
                .Any();

            if (used)
                throw new ConflictException("Product referenced by orders.");

            await _productRepo.Delete(id);
            return true;
        }

        // ============================================================
        // GET BY ID (IQueryable + Include)
        // ============================================================
        public async Task<ProductReadDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var product = await _productRepo.GetQueryable()
                .AsNoTracking()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (product is null)
                throw new NotFoundException("Product not found.");

            var dto = _mapper.Map<ProductReadDto>(product);

            var ratings = product.Reviews;
            dto.AverageRating = ratings.Any() ? Math.Round(ratings.Average(r => r.Rating), 2) : 0;
            dto.ReviewsCount = ratings.Count;

            return dto;
        }

        // ============================================================
        // GET ALL (IQueryable + Include + Sorting)
        // ============================================================
        public async Task<PagedResult<ProductReadDto>> GetAllAsync(int page, int size,
            string? sortBy = "newest", string? sortDir = "desc", CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            size = Math.Max(1, size);

            var q = _productRepo.GetQueryable()
                .AsNoTracking()
                .Include(p => p.Images);

            bool desc = sortDir == "desc";

            IQueryable<Product> sorted = (sortBy?.ToLowerInvariant()) switch
            {
                "price" => desc ? q.OrderByDescending(p => p.Price) : q.OrderBy(p => p.Price),
                "name" => desc ? q.OrderByDescending(p => p.Name) : q.OrderBy(p => p.Name),
                _ => desc ? q.OrderByDescending(p => p.Id) : q.OrderBy(p => p.Id),
            };

            var total = await sorted.CountAsync(ct);

            var data = await sorted
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            var items = data.Select(p =>
            {
                var dto = _mapper.Map<ProductReadDto>(p);
                dto.AverageRating = p.Reviews.Any() ? Math.Round(p.Reviews.Average(r => r.Rating), 2) : 0;
                dto.ReviewsCount = p.Reviews.Count;
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

        // ============================================================
        // SEARCH (IQueryable)
        // ============================================================
        public async Task<PagedResult<ProductReadDto>> SearchAsync(ProductQuery query, CancellationToken ct = default)
        {
            var q = _productRepo.GetQueryable()
                .Include(p => p.Images)
                .AsNoTracking()
                .Where(p => p.IsActive);

            if (query.CategoryId.HasValue)
                q = q.Where(p => p.CategoryId == query.CategoryId.Value);

            if (!string.IsNullOrWhiteSpace(query.NameContains))
                q = q.Where(p => p.Name.Contains(query.NameContains.Trim()));

            if (query.PriceMin.HasValue)
                q = q.Where(p => p.Price >= query.PriceMin.Value);

            if (query.PriceMax.HasValue)
                q = q.Where(p => p.Price <= query.PriceMax.Value);

            bool desc = query.SortDir == "desc";

            IQueryable<Product> sorted = (query.SortBy?.ToLowerInvariant()) switch
            {
                "price" => desc ? q.OrderByDescending(p => p.Price) : q.OrderBy(p => p.Price),
                "name" => desc ? q.OrderByDescending(p => p.Name) : q.OrderBy(p => p.Name),
                _ => desc ? q.OrderByDescending(p => p.CreatedUtc) : q.OrderBy(p => p.CreatedUtc),
            };

            var total = await sorted.CountAsync(ct);

            var data = await sorted
                .Skip((query.Page - 1) * query.Size)
                .Take(query.Size)
                .ToListAsync(ct);

            var items = data.Select(p =>
            {
                var dto = _mapper.Map<ProductReadDto>(p);
                dto.AverageRating = p.Reviews.Any() ? Math.Round(p.Reviews.Average(r => r.Rating), 2) : 0;
                dto.ReviewsCount = p.Reviews.Count;
                return dto;
            }).ToList();

            return new PagedResult<ProductReadDto>
            {
                Items = items,
                PageNumber = query.Page,
                PageSize = query.Size,
                TotalCount = total
            };
        }

        // ============================================================
        // ADD IMAGE (REPO)
        // ============================================================
        public async Task AddImageAsync(int productId, ProductImageCreateDto dto, CancellationToken ct = default)
        {
            var entity = await _productRepo.Get(productId);
            if (entity is null)
                throw new NotFoundException("Product not found.");

            await _imageRepo.Add(new ProductImage
            {
                ProductId = productId,
                Url = dto.Url.Trim()
            });
        }

        // ============================================================
        // REMOVE IMAGE (REPO)
        // ============================================================
        public async Task<bool> RemoveImageAsync(int productId, int imageId, CancellationToken ct = default)
        {
            var image = await _imageRepo.Get(imageId);
            if (image == null || image.ProductId != productId)
                throw new NotFoundException("Product image not found.");

            await _imageRepo.Delete(imageId);
            return true;
        }

        // ============================================================
        // SET ACTIVE (REPO)
        // ============================================================
        public async Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
        {
            var product = await _productRepo.Get(id);
            if (product == null)
                throw new NotFoundException("Product not found.");

            product.IsActive = isActive;
            product.UpdatedUtc = DateTime.UtcNow;

            await _productRepo.Update(id, product);
        }

        // ============================================================
        // GET REVIEWS BY PRODUCT (IQueryable)
        // ============================================================
        public async Task<PagedResult<ReviewReadDto>> GetReviewsByProductIdAsync(int productId, int page, int size, int? minRating = null, string? sortBy = "newest", string? sortDir = "desc", CancellationToken ct = default)
        {
            var product = await _productRepo.Get(productId);
            if (product == null)
                throw new NotFoundException("Product not found.");

            var q = _productRepo.GetQueryable()
                .Where(p => p.Id == productId)
                .SelectMany(p => p.Reviews)
                .AsQueryable();

            if (minRating.HasValue)
                q = q.Where(r => r.Rating >= minRating.Value);

            bool desc = sortDir == "desc";

            q = (sortBy?.ToLowerInvariant()) switch
            {
                "rating" => desc ? q.OrderByDescending(r => r.Rating) : q.OrderBy(r => r.Rating),
                _ => desc ? q.OrderByDescending(r => r.CreatedUtc) : q.OrderBy(r => r.CreatedUtc)
            };

            var total = await q.CountAsync(ct);

            var data = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);

            var items = data.Select(_mapper.Map<ReviewReadDto>).ToList();

            return new PagedResult<ReviewReadDto>
            {
                Items = items,
                PageNumber = page,
                PageSize = size,
                TotalCount = total
            };
        }
    }

}