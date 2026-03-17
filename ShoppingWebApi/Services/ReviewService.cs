using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Reviews;

namespace ShoppingWebApi.Services
{
    public class ReviewService : IReviewService
    {
        private readonly IRepository<int, Review> _reviewRepo;
        private readonly IRepository<int, Product> _productRepo;
        private readonly IRepository<int, User> _userRepo;
        private readonly IMapper _mapper;
        private readonly ILogger<ReviewService> _logger;

        public ReviewService(
            IRepository<int, Review> reviewRepo,
            IRepository<int, Product> productRepo,
            IRepository<int, User> userRepo,
            IMapper mapper,
            ILogger<ReviewService> logger)
        {
            _reviewRepo = reviewRepo;
            _productRepo = productRepo;
            _userRepo = userRepo;
            _mapper = mapper;
            _logger = logger;
        }

        // ---------------------------------------------------------
        // CREATE REVIEW (repo + queryable)
        // ---------------------------------------------------------
        public async Task<ReviewReadDto> CreateAsync(ReviewCreateDto dto, CancellationToken ct = default)
        {
            // validate product exists
            var product = await _productRepo.Get(dto.ProductId);
            if (product is null) throw new NotFoundException("Product not found.");

            // validate user
            var user = await _userRepo.Get(dto.UserId);
            if (user is null) throw new NotFoundException("User not found.");

            // check duplicate review
            var duplicate = await _reviewRepo.GetQueryable()
                .AnyAsync(r => r.ProductId == dto.ProductId && r.UserId == dto.UserId, ct);

            if (duplicate)
                throw new ConflictException("User already reviewed this product.");

            if (dto.Rating < 1 || dto.Rating > 5)
                throw new BusinessValidationException("Rating must be between 1 and 5.");

            var entity = new Review
            {
                ProductId = dto.ProductId,
                UserId = dto.UserId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedUtc = DateTime.UtcNow
            };

            await _reviewRepo.Add(entity);

            return _mapper.Map<ReviewReadDto>(entity);
        }

        // ---------------------------------------------------------
        // GET REVIEW BY PRODUCT + USER (repo queryable)
        // ---------------------------------------------------------
        public async Task<ReviewReadDto?> GetAsync(int productId, int userId, CancellationToken ct = default)
        {
            var r = await _reviewRepo.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProductId == productId && x.UserId == userId, ct);

            return r == null ? null : _mapper.Map<ReviewReadDto>(r);
        }

        // ---------------------------------------------------------
        // GET REVIEWS BY PRODUCT (repo queryable + paging)
        // ---------------------------------------------------------
        public async Task<PagedResult<ReviewReadDto>> GetByProductAsync(
            int productId, int page = 1, int size = 10, CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            size = Math.Max(1, size);

            var query = _reviewRepo.GetQueryable()
                .AsNoTracking()
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedUtc);

            var total = await query.CountAsync(ct);

            var items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .Select(r => _mapper.Map<ReviewReadDto>(r))
                .ToListAsync(ct);

            return new PagedResult<ReviewReadDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = size
            };
        }

        // ---------------------------------------------------------
        // UPDATE REVIEW (repo)
        // ---------------------------------------------------------
        public async Task<bool> UpdateAsync(int productId, int userId, ReviewUpdateDto dto, CancellationToken ct = default)
        {
            var review = await _reviewRepo.GetQueryable()
                .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId, ct);

            if (review is null)
                throw new NotFoundException("Review not found.");

            if (dto.Rating < 1 || dto.Rating > 5)
                throw new BusinessValidationException("Rating must be between 1 and 5.");

            review.Rating = dto.Rating;
            review.Comment = dto.Comment;
            review.UpdatedUtc = DateTime.UtcNow;

            await _reviewRepo.Update(review.Id, review);
            return true;
        }

        // ---------------------------------------------------------
        // DELETE REVIEW (repo)
        // ---------------------------------------------------------
        public async Task<bool> DeleteAsync(int productId, int userId, CancellationToken ct = default)
        {
            var review = await _reviewRepo.GetQueryable()
                .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId, ct);

            if (review == null)
                return false;

            await _reviewRepo.Delete(review.Id);
            return true;
        }
    }
}