using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Categories;
using ShoppingWebApi.Models.DTOs.Common;

namespace ShoppingWebApi.Services
{
    public class CategoryService : ICategoryService
    {
        // Repos for all CRUD paths
        private readonly IRepository<int, Category> _categoryRepo;
        private readonly IRepository<int, Product> _productRepo;

        // DbContext ONLY for complex read (server-side paging/sorting in GetAll)
        private readonly AppDbContext _db;

        private readonly IMapper _mapper;

        public CategoryService(
            IRepository<int, Category> categoryRepo,
            IRepository<int, Product> productRepo,
            AppDbContext db,
            IMapper mapper)
        {
            _categoryRepo = categoryRepo;
            _productRepo = productRepo;
            _db = db;       // used only in GetAll (read-only)
            _mapper = mapper;
        }

        // -----------------------------
        // Create (repo-only)
        // -----------------------------
        public async Task<CategoryReadDto> CreateAsync(CategoryCreateDto dto, CancellationToken ct = default)
        {
            // If parent provided, ensure it exists
            if (dto.ParentCategoryId.HasValue)
            {
                var parent = await _categoryRepo.Get(dto.ParentCategoryId.Value);
                if (parent is null)
                    throw new NotFoundException("Parent category not found.");
            }

            var entity = new Category
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                ParentCategoryId = dto.ParentCategoryId
            };

            var added = await _categoryRepo.Add(entity);
            if (added is null)
                throw new BusinessValidationException("Failed to create category.");

            return _mapper.Map<CategoryReadDto>(added);
        }

        // -----------------------------
        // Update (repo-only)
        // -----------------------------
        public async Task<CategoryReadDto?> UpdateAsync(int id, CategoryUpdateDto dto, CancellationToken ct = default)
        {
            if (id != dto.Id)
                throw new BusinessValidationException(
                    "Route id and payload id do not match.",
                    new Dictionary<string, string[]> { ["id"] = new[] { "Mismatch" } });

            var entity = await _categoryRepo.Get(id);
            if (entity is null)
                throw new NotFoundException("Category not found.");

            // Validate parent usage (self/exists/cycle)
            if (dto.ParentCategoryId.HasValue)
            {
                var newParentId = dto.ParentCategoryId.Value;

                if (newParentId == id)
                    throw new BusinessValidationException(
                        "A category cannot be its own parent.",
                        new Dictionary<string, string[]> { ["parentCategoryId"] = new[] { "Self-parenting is not allowed." } });

                var parent = await _categoryRepo.Get(newParentId);
                if (parent is null)
                    throw new NotFoundException("Parent category not found.");

                // Prevent cycle: walk up the parent chain
                int? currentParentId = parent.ParentCategoryId;
                while (currentParentId.HasValue)
                {
                    if (currentParentId.Value == id)
                        throw new BusinessValidationException(
                            "Setting this parent would create a cycle.",
                            new Dictionary<string, string[]> { ["parentCategoryId"] = new[] { "Cyclic parent assignment." } });

                    var ancestor = await _categoryRepo.Get(currentParentId.Value);
                    currentParentId = ancestor?.ParentCategoryId;
                }
            }

            entity.Name = dto.Name.Trim();
            entity.Description = dto.Description?.Trim();
            entity.ParentCategoryId = dto.ParentCategoryId;
            entity.UpdatedUtc = DateTime.UtcNow;

            var updated = await _categoryRepo.Update(id, entity);
            if (updated is null)
                throw new NotFoundException("Category not found after update.");

            return _mapper.Map<CategoryReadDto>(updated);
        }

        // -----------------------------
        // Delete (repo-only, with guards)
        // -----------------------------
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var existing = await _categoryRepo.Get(id);
            if (existing is null)
                throw new NotFoundException("Category not found.");

            // Guard: cannot delete if has children
            var allCategories = await _categoryRepo.GetAll() ?? Enumerable.Empty<Category>();
            if (allCategories.Any(c => c.ParentCategoryId == id))
                throw new ConflictException("Category has child categories and cannot be deleted.");

            // Guard: cannot delete if has products
            var allProducts = await _productRepo.GetAll() ?? Enumerable.Empty<Product>();
            if (allProducts.Any(p => p.CategoryId == id))
                throw new ConflictException("Category has products and cannot be deleted.");

            var deleted = await _categoryRepo.Delete(id);
            if (deleted is null)
                throw new NotFoundException("Category not found.");

            return true; // Controller can return 204 No Content
        }

        // -----------------------------
        // GetById (repo-only)
        // -----------------------------
        public async Task<CategoryReadDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _categoryRepo.Get(id);
            if (entity is null)
                throw new NotFoundException("Category not found.");

            return _mapper.Map<CategoryReadDto>(entity);
        }

        // --------------------------------------------------------
        // GetAll (server-side paging/sorting) -> DbContext READ ONLY
        // --------------------------------------------------------
        public async Task<PagedResult<CategoryReadDto>> GetAllAsync(
            int page, int size, string? sortBy = "name", string? sortDir = "asc", CancellationToken ct = default)
        {
            page = page <= 0 ? 1 : page;
            size = size <= 0 ? 10 : size;

            var query = _db.Categories.AsNoTracking();

            // Sorting (server-side)
            bool desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = (sortBy?.ToLowerInvariant()) switch
            {
                "createdutc" => desc ? query.OrderByDescending(c => c.CreatedUtc) : query.OrderBy(c => c.CreatedUtc),
                "updatedutc" => desc ? query.OrderByDescending(c => c.UpdatedUtc) : query.OrderBy(c => c.UpdatedUtc),
                "name" or _ => desc ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            };

            // Total + page slice (server-side)
            var total = await query.CountAsync(ct);
            var data = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            return new PagedResult<CategoryReadDto>
            {
                Items = data.Select(_mapper.Map<CategoryReadDto>).ToList(),
                PageNumber = page,
                PageSize = size,
                TotalCount = total
            };
        }
    }
}