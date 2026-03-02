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
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;

        public CategoryService(AppDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        public async Task<CategoryReadDto> CreateAsync(CategoryCreateDto dto, CancellationToken ct = default)
        {
            // If parent provided, ensure it exists
            if (dto.ParentCategoryId.HasValue)
            {
                var parentExists = await _db.Categories
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == dto.ParentCategoryId.Value, ct);
                if (!parentExists)
                    throw new NotFoundException("Parent category not found.");
            }

            var entity = new Category
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                ParentCategoryId = dto.ParentCategoryId
            };

            _db.Categories.Add(entity);
            await _db.SaveChangesAsync(ct);

            return _mapper.Map<CategoryReadDto>(entity);
        }

        public async Task<CategoryReadDto?> UpdateAsync(int id, CategoryUpdateDto dto, CancellationToken ct = default)
        {
            if (id != dto.Id)
                throw new BusinessValidationException("Route id and payload id do not match.",
                    new Dictionary<string, string[]> { ["id"] = new[] { "Mismatch" } });

            var entity = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (entity == null)
                throw new NotFoundException("Category not found.");

            // Validate parent usage
            if (dto.ParentCategoryId.HasValue)
            {
                if (dto.ParentCategoryId.Value == id)
                    throw new BusinessValidationException("A category cannot be its own parent.",
                        new Dictionary<string, string[]> { ["parentCategoryId"] = new[] { "Self-parenting is not allowed." } });

                // Ensure parent exists
                var parent = await _db.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == dto.ParentCategoryId.Value, ct);

                if (parent == null)
                    throw new NotFoundException("Parent category not found.");

                // Prevent cycle: walk up the parent chain from the chosen parent
                var currentParentId = parent.ParentCategoryId;
                while (currentParentId.HasValue)
                {
                    if (currentParentId.Value == id)
                        throw new BusinessValidationException("Setting this parent would create a cycle.",
                            new Dictionary<string, string[]> { ["parentCategoryId"] = new[] { "Cyclic parent assignment." } });

                    currentParentId = await _db.Categories
                        .Where(c => c.Id == currentParentId.Value)
                        .Select(c => c.ParentCategoryId)
                        .FirstOrDefaultAsync(ct);
                }
            }

            entity.Name = dto.Name.Trim();
            entity.Description = dto.Description?.Trim();
            entity.ParentCategoryId = dto.ParentCategoryId;
            entity.UpdatedUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            return _mapper.Map<CategoryReadDto>(entity);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.Categories
                .Include(c => c.Children)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (entity == null)
                throw new NotFoundException("Category not found.");

            // Guard: cannot delete if has children
            if (entity.Children.Any())
                throw new ConflictException("Category has child categories and cannot be deleted.");

            // Guard: cannot delete if has products
            var hasProducts = await _db.Products.AsNoTracking().AnyAsync(p => p.CategoryId == id, ct);
            if (hasProducts)
                throw new ConflictException("Category has products and cannot be deleted.");

            _db.Categories.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<CategoryReadDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
            if (entity == null)
                throw new NotFoundException("Category not found.");
            return _mapper.Map<CategoryReadDto>(entity);
        }

        public async Task<PagedResult<CategoryReadDto>> GetAllAsync(
            int page, int size, string? sortBy = "name", string? sortDir = "asc", CancellationToken ct = default)
        {
            page = page <= 0 ? 1 : page;
            size = size <= 0 ? 10 : size;

            var query = _db.Categories.AsNoTracking();

            // Sorting
            var dirDesc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = (sortBy?.ToLowerInvariant()) switch
            {
                "createdutc" => dirDesc ? query.OrderByDescending(c => c.CreatedUtc) : query.OrderBy(c => c.CreatedUtc),
                "updatedutc" => dirDesc ? query.OrderByDescending(c => c.UpdatedUtc) : query.OrderBy(c => c.UpdatedUtc),
                "name" or _ => dirDesc ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            };

            var total = await query.CountAsync(ct);
            var data = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            var items = data.Select(_mapper.Map<CategoryReadDto>).ToList();

            return new PagedResult<CategoryReadDto>
            {
                Items = items,
                PageNumber = page,
                PageSize = size,
                TotalCount = total
            };
        }
    }
}