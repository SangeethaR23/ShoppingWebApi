using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Reviews;

namespace ShoppingWebApi.Services
{
    public class ReviewService : IReviewService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ReviewService> _logger;

        public ReviewService(AppDbContext db, ILogger<ReviewService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<ReviewReadDto> CreateAsync(ReviewCreateDto dto, CancellationToken ct = default)
        {
            var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == dto.UserId, ct);
            if (!userExists) throw new NotFoundException("User not found.");

            var productExists = await _db.Products.AsNoTracking().AnyAsync(p => p.Id == dto.ProductId, ct);
            if (!productExists) throw new NotFoundException("Product not found.");

            // 1 per user per product — enforced by unique index; we also check
            var exists = await _db.Reviews.AsNoTracking()
                .AnyAsync(r => r.ProductId == dto.ProductId && r.UserId == dto.UserId, ct);
            if (exists) throw new ConflictException("You have already reviewed this product.");

            if (dto.Rating < 1 || dto.Rating > 5)
                throw new BusinessValidationException("Rating must be between 1 and 5.");

            var entity = new Review
            {
                ProductId = dto.ProductId,
                UserId = dto.UserId,
                Rating = dto.Rating,
                Comment = dto.Comment
            };

            await _db.Reviews.AddAsync(entity, ct);
            await _db.SaveChangesAsync(ct);

            return ToReadDto(entity);
        }

        public async Task<ReviewReadDto?> GetAsync(int productId, int userId, CancellationToken ct = default)
        {
            var r = await _db.Reviews.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProductId == productId && x.UserId == userId, ct);
            return r == null ? null : ToReadDto(r);
        }

        public async Task<PagedResult<ReviewReadDto>> GetByProductAsync(int productId, int page = 1, int size = 10, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (size < 1) size = 10;

            var q = _db.Reviews.AsNoTracking()
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedUtc);

            var total = await q.CountAsync(ct);

            var items = await q.Skip((page - 1) * size).Take(size)
                .Select(r => new ReviewReadDto
                {
                    ProductId = r.ProductId,
                    UserId = r.UserId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedUtc = r.CreatedUtc
                })
                .ToListAsync(ct);

            return new PagedResult<ReviewReadDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = size
            };
        }

        public async Task<bool> UpdateAsync(int productId, int userId, ReviewUpdateDto dto, CancellationToken ct = default)
        {
            var r = await _db.Reviews.FirstOrDefaultAsync(x => x.ProductId == productId && x.UserId == userId, ct);
            if (r == null) throw new NotFoundException("Review not found.");

            if (dto.Rating < 1 || dto.Rating > 5)
                throw new BusinessValidationException("Rating must be between 1 and 5.");

            r.Rating = dto.Rating;
            r.Comment = dto.Comment;
            r.UpdatedUtc = System.DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> DeleteAsync(int productId, int userId, CancellationToken ct = default)
        {
            var r = await _db.Reviews.FirstOrDefaultAsync(x => x.ProductId == productId && x.UserId == userId, ct);
            if (r == null) return false;

            _db.Reviews.Remove(r);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        private static ReviewReadDto ToReadDto(Review r) => new ReviewReadDto
        {
            ProductId = r.ProductId,
            UserId = r.UserId,
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedUtc = r.CreatedUtc
        };
    }
}