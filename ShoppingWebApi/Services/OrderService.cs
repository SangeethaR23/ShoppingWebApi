using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Orders;
using ShoppingWebApi.Exceptions;

namespace ShoppingWebApi.Services
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<OrderService> _logger;

        public OrderService(AppDbContext db, ILogger<OrderService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<PlaceOrderResponseDto> PlaceOrderAsync(PlaceOrderRequestDto request, CancellationToken ct = default)
        {
            // Validate user
            var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == request.UserId, ct);
            if (!userExists) throw new NotFoundException($"User {request.UserId} not found.");

            // Validate address belongs to user & snapshot it
            var address = await _db.Addresses.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.AddressId && a.UserId == request.UserId, ct);
            if (address == null) throw new BusinessValidationException("Invalid address for this user.");

            // Load cart with items and product (for Name + SKU when missing from snapshot)
            var cart = await _db.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserId == request.UserId, ct);

            if (cart == null || cart.Items.Count == 0)
                throw new BusinessValidationException("Cart is empty.");

            // Build lines from cart snapshot
            var lines = cart.Items.Select(i => new
            {
                CartItemId = i.Id,
                i.ProductId,
                i.Quantity,
                // Prefer snapshot UnitPrice; fallback to current product price
                UnitPrice = i.UnitPrice > 0 ? i.UnitPrice : (i.Product?.Price ?? 0m),
                ProductName = i.Product?.Name ?? string.Empty,
                SKU = i.Product?.SKU ?? string.Empty
            }).ToList();

            if (lines.Any(l => l.UnitPrice <= 0))
                throw new BusinessValidationException("One or more products have invalid unit price.");

            // Validate inventory (1:1 Product ↔ Inventory)
            var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
            var invMap = await _db.Inventories
                .Where(inv => productIds.Contains(inv.ProductId))
                .ToDictionaryAsync(inv => inv.ProductId, ct);

            foreach (var l in lines)
            {
                if (!invMap.TryGetValue(l.ProductId, out var inv))
                    throw new BusinessValidationException($"Inventory missing for product {l.ProductId}.");

                if (inv.Quantity < l.Quantity)
                    throw new BusinessValidationException($"Insufficient inventory for product {l.ProductId}. " +
                                                          $"Available: {inv.Quantity}, Requested: {l.Quantity}");
            }

            // Totals
            var subTotal = lines.Sum(l => l.UnitPrice * l.Quantity);
            var shipping = request.ShippingFee ?? 0m;
            var discount = request.Discount ?? 0m;
            var total = subTotal + shipping - discount;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Unique OrderNumber
                var orderNumber = await GenerateUniqueOrderNumberAsync(ct);

                var order = new Order
                {
                    UserId = request.UserId,
                    OrderNumber = orderNumber,
                    Status = OrderStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending,
                    PlacedAtUtc = DateTime.UtcNow,

                    // Shipping snapshot from Address
                    ShipToName = address.FullName,
                    ShipToPhone = address.Phone,
                    ShipToLine1 = address.Line1,
                    ShipToLine2 = address.Line2,
                    ShipToCity = address.City,
                    ShipToState = address.State,
                    ShipToPostalCode = address.PostalCode,
                    ShipToCountry = address.Country,

                    // Money
                    SubTotal = subTotal,
                    ShippingFee = shipping,
                    Discount = discount,
                    Total = total
                };

                await _db.Orders.AddAsync(order, ct);
                await _db.SaveChangesAsync(ct); // generate order.Id

                // Create OrderItems & decrement inventory
                var orderItems = new List<OrderItem>(capacity: lines.Count);
                foreach (var l in lines)
                {
                    var inv = invMap[l.ProductId];
                    inv.Quantity -= l.Quantity;
                    _db.Inventories.Update(inv);

                    var lineTotal = l.UnitPrice * l.Quantity;

                    orderItems.Add(new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = l.ProductId,
                        ProductName = l.ProductName,
                        SKU = l.SKU,
                        UnitPrice = l.UnitPrice,
                        Quantity = l.Quantity,
                        LineTotal = lineTotal
                    });
                }

                await _db.OrderItems.AddRangeAsync(orderItems, ct);

                // Clear cart
                _db.CartItems.RemoveRange(cart.Items);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return new PlaceOrderResponseDto
                {
                    Id = order.Id,
                    OrderNumber = order.OrderNumber,
                    Total = order.Total,
                    Status = order.Status.ToString(),
                    PaymentStatus = order.PaymentStatus.ToString(),
                    PlacedAtUtc = order.PlacedAtUtc
                };
            }
            catch (Exception ex)
            {
                await LogErrorToDbAsync(ex, "OrderService.PlaceOrder", ct);
                _logger.LogError(ex, "Failed to place order for user {UserId}", request.UserId);
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<OrderReadDto?> GetByIdAsync(int orderId, CancellationToken ct = default)
        {
            var order = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (order == null) return null;

            var items = order.Items.Select(oi => new OrderDetailDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
                ProductName = oi.ProductName,
                SKU = oi.SKU,
                UnitPrice = oi.UnitPrice,
                Quantity = oi.Quantity,
                LineTotal = oi.LineTotal
            }).ToList();

            return new OrderReadDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status.ToString(),
                PaymentStatus = order.PaymentStatus.ToString(),
                PlacedAtUtc = order.PlacedAtUtc,

                ShipToName = order.ShipToName,
                ShipToPhone = order.ShipToPhone,
                ShipToLine1 = order.ShipToLine1,
                ShipToLine2 = order.ShipToLine2,
                ShipToCity = order.ShipToCity,
                ShipToState = order.ShipToState,
                ShipToPostalCode = order.ShipToPostalCode,
                ShipToCountry = order.ShipToCountry,

                SubTotal = order.SubTotal,
                ShippingFee = order.ShippingFee,
                Discount = order.Discount,
                Total = order.Total,

                Items = items
            };
        }

        public async Task<PagedResult<OrderSummaryDto>> GetUserOrdersAsync(
            int userId, int page = 1, int size = 10, string? sortBy = "date", bool desc = true, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (size < 1) size = 10;

            var q = _db.Orders.AsNoTracking().Where(o => o.UserId == userId);

            q = (sortBy?.ToLowerInvariant()) switch
            {
                "total" => desc ? q.OrderByDescending(o => o.Total) : q.OrderBy(o => o.Total),
                "status" => desc ? q.OrderByDescending(o => o.Status) : q.OrderBy(o => o.Status),
                _ => desc ? q.OrderByDescending(o => o.PlacedAtUtc) : q.OrderBy(o => o.PlacedAtUtc),
            };

            var totalCount = await q.CountAsync(ct);

            var rows = await q.Skip((page - 1) * size).Take(size)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.Status,
                    o.PlacedAtUtc,
                    o.Total,
                    ItemsCount = _db.OrderItems.Count(oi => oi.OrderId == o.Id)
                })
                .ToListAsync(ct);

            var items = rows.Select(r => new OrderSummaryDto
            {
                Id = r.Id,
                OrderNumber = r.OrderNumber,
                Status = r.Status.ToString(),
                PlacedAtUtc = r.PlacedAtUtc,
                Total = r.Total,
                ItemsCount = r.ItemsCount
            }).ToList();

            return new PagedResult<OrderSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = size
            };
        }

        public async Task<CancelOrderResponseDto> CancelOrderAsync(
            int orderId, int userId, bool isAdmin = false, string? reason = null, CancellationToken ct = default)
        {
            var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null) throw new NotFoundException("Order not found.");

            if (!isAdmin && order.UserId != userId)
                throw new ForbiddenException("You cannot cancel another user's order.");

            if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
                throw new BusinessValidationException($"Order already {order.Status}, cannot cancel.");

            if (order.Status == OrderStatus.Cancelled)
                return new CancelOrderResponseDto { Id = order.Id, Status = order.Status.ToString(), Message = "Order already cancelled." };

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Restore inventory
                var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
                var invMap = await _db.Inventories.Where(inv => productIds.Contains(inv.ProductId))
                    .ToDictionaryAsync(inv => inv.ProductId, ct);

                foreach (var i in order.Items)
                {
                    if (invMap.TryGetValue(i.ProductId, out var inv))
                    {
                        inv.Quantity += i.Quantity;
                        _db.Inventories.Update(inv);
                    }
                }

                order.Status = OrderStatus.Cancelled;

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return new CancelOrderResponseDto
                {
                    Id = order.Id,
                    Status = order.Status.ToString(),
                    Message = "Order cancelled and inventory restored."
                };
            }
            catch (Exception ex)
            {
                await LogErrorToDbAsync(ex, "OrderService.CancelOrder", ct);
                _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<PagedResult<OrderReadDto>> GetAllAsync(
            string? status = null, DateTime? from = null, DateTime? to = null, int? userId = null,
            int page = 1, int size = 10, string? sortBy = "date", bool desc = true, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (size < 1) size = 10;

            var q = _db.Orders.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var st))
                q = q.Where(o => o.Status == st);
            if (userId.HasValue) q = q.Where(o => o.UserId == userId.Value);
            if (from.HasValue) q = q.Where(o => o.PlacedAtUtc >= from.Value);
            if (to.HasValue) q = q.Where(o => o.PlacedAtUtc <= to.Value);

            q = (sortBy?.ToLowerInvariant()) switch
            {
                "total" => desc ? q.OrderByDescending(o => o.Total) : q.OrderBy(o => o.Total),
                "status" => desc ? q.OrderByDescending(o => o.Status) : q.OrderBy(o => o.Status),
                _ => desc ? q.OrderByDescending(o => o.PlacedAtUtc) : q.OrderBy(o => o.PlacedAtUtc),
            };

            var totalCount = await q.CountAsync(ct);
            var orders = await q
                .Skip((page - 1) * size).Take(size)
                .Include(o => o.Items)
                .ToListAsync(ct);

            var items = orders.Select(order => new OrderReadDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status.ToString(),
                PaymentStatus = order.PaymentStatus.ToString(),
                PlacedAtUtc = order.PlacedAtUtc,

                ShipToName = order.ShipToName,
                ShipToPhone = order.ShipToPhone,
                ShipToLine1 = order.ShipToLine1,
                ShipToLine2 = order.ShipToLine2,
                ShipToCity = order.ShipToCity,
                ShipToState = order.ShipToState,
                ShipToPostalCode = order.ShipToPostalCode,
                ShipToCountry = order.ShipToCountry,

                SubTotal = order.SubTotal,
                ShippingFee = order.ShippingFee,
                Discount = order.Discount,
                Total = order.Total,

                Items = order.Items.Select(oi => new OrderDetailDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.ProductName,
                    SKU = oi.SKU,
                    UnitPrice = oi.UnitPrice,
                    Quantity = oi.Quantity,
                    LineTotal = oi.LineTotal
                }).ToList()
            }).ToList();

            return new PagedResult<OrderReadDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = size
            };
        }

        public async Task<bool> UpdateStatusAsync(int orderId, string newStatus, CancellationToken ct = default)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null) return false;

            if (!Enum.TryParse<OrderStatus>(newStatus, true, out var parsed))
                throw new BusinessValidationException("Invalid order status value.");

            if (order.Status == OrderStatus.Cancelled)
                throw new ConflictException("Cancelled orders cannot change status.");

            order.Status = parsed;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // --- Helpers ---

        private async Task<string> GenerateUniqueOrderNumberAsync(CancellationToken ct)
        {
            // e.g., ORD-20260301-AB12CD34
            string ord;
            do
            {
                var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()).Substring(0, 8);
                ord = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{token}";
            }
            while (await _db.Orders.AsNoTracking().AnyAsync(o => o.OrderNumber == ord, ct));
            return ord;
        }

        private async Task LogErrorToDbAsync(Exception ex, string source, CancellationToken ct)
        {
            try
            {
                await _db.Logs.AddAsync(new LogEntry
                {
                    Level = "Error",
                    Message = ex.Message,
                    Exception = ex.GetType().FullName,
                    StackTrace = ex.StackTrace,
                    Source = source,
                    CreatedUtc = DateTime.UtcNow
                }, ct);
                await _db.SaveChangesAsync(ct);
            }
            catch
            {
                // swallow logging failures
            }
        }
    }
}