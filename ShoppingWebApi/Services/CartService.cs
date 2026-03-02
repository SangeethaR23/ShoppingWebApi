using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Cart;

namespace ShoppingWebApi.Services
{
    public class CartService : ICartService
    {
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;

        public CartService(AppDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        public async Task<CartReadDto> GetByUserIdAsync(int userId, CancellationToken ct = default)
        {
            var cart = await _db.Carts
                .AsNoTracking()
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);

            if (cart == null)
            {
                // Return empty cart, do not create row on GET
                return new CartReadDto { Id = 0, UserId = userId, Items = new(), SubTotal = 0m };
            }

            var dto = _mapper.Map<CartReadDto>(cart);

            if (dto.Items.Count > 0)
            {
                var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();

                var ratingLookup = await _db.Reviews
                    .Where(r => productIds.Contains(r.ProductId))
                    .GroupBy(r => r.ProductId)
                    .Select(g => new { ProductId = g.Key, Avg = g.Average(x => (double)x.Rating), Count = g.Count() })
                    .ToDictionaryAsync(x => x.ProductId, x => (avg: x.Avg, count: x.Count), ct);

                foreach (var item in dto.Items)
                {
                    if (ratingLookup.TryGetValue(item.ProductId, out var r))
                    {
                        item.AverageRating = Math.Round(r.avg, 2);
                        item.ReviewsCount = r.count;
                    }
                }

                dto.SubTotal = dto.Items.Sum(i => i.LineTotal);
            }

            return dto;
        }

        public async Task<CartReadDto> AddItemAsync(int userId, CartAddItemDto dto, CancellationToken ct = default)
        {
            // Ensure user exists (optional if enforced by auth)
            var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, ct);
            if (!userExists) throw new NotFoundException("User not found.");

            // Ensure product exists and is active
            var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == dto.ProductId, ct);
            if (product == null) throw new NotFoundException("Product not found.");
            if (!product.IsActive) throw new BusinessValidationException("Product is inactive and cannot be added to cart.");

            // Get or create cart
            var cart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);

            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _db.Carts.Add(cart);
                await _db.SaveChangesAsync(ct); // to get Cart.Id for FK
                // Reload with Items for consistency
                cart = await _db.Carts.Include(c => c.Items).FirstAsync(c => c.UserId == userId, ct);
            }

            // Check if item exists
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == dto.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += dto.Quantity;
                existingItem.UpdatedUtc = DateTime.UtcNow;
            }
            else
            {
                // Snapshot current price
                var price = product.Price;
                var newItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity,
                    UnitPrice = price
                };
                cart.Items.Add(newItem);
            }

            await _db.SaveChangesAsync(ct);

            // Return fresh read dto
            return await GetByUserIdAsync(userId, ct);
        }

        public async Task<CartReadDto> UpdateItemAsync(int userId, CartUpdateItemDto dto, CancellationToken ct = default)
        {
            var cart = await _db.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);

            if (cart == null) throw new NotFoundException("Cart not found.");

            var item = cart.Items.FirstOrDefault(i => i.ProductId == dto.ProductId);
            if (item == null) throw new NotFoundException("Cart item not found.");

            if (dto.Quantity == 0)
            {
                _db.CartItems.Remove(item);
            }
            else
            {
                item.Quantity = dto.Quantity;
                item.UpdatedUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            return await GetByUserIdAsync(userId, ct);
        }

        public async Task RemoveItemAsync(int userId, int productId, CancellationToken ct = default)
        {
            var cart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);

            if (cart == null) throw new NotFoundException("Cart not found.");

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item == null) throw new NotFoundException("Cart item not found.");

            _db.CartItems.Remove(item);
            await _db.SaveChangesAsync(ct);
        }

        public async Task ClearAsync(int userId, CancellationToken ct = default)
        {
            var cart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);

            if (cart == null) return;

            _db.CartItems.RemoveRange(cart.Items);
            await _db.SaveChangesAsync(ct);
        }
    }
}