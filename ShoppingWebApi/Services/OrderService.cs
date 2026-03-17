using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Orders;
using ShoppingWebApi.Models.enums; 

namespace ShoppingWebApi.Services
{
    public class OrderService : IOrderService
    {
        private readonly IRepository<int, User> _userRepo;
        private readonly IRepository<int, Address> _addressRepo;
        private readonly IRepository<int, Cart> _cartRepo;
        private readonly IRepository<int, CartItem> _cartItemRepo;
        private readonly IRepository<int, Order> _orderRepo;
        private readonly IRepository<int, OrderItem> _orderItemRepo;
        private readonly IRepository<int, Inventory> _inventoryRepo;
        private readonly IRepository<int, Payment> _paymentRepo;
        private readonly IRepository<int, Refund> _refundRepo;

        private readonly ILogWriter _loggerDb;                 // DB logger (minimal)
        private readonly ILogger<OrderService> _logger;         // Console/app logger (optional)

        public OrderService(
            IRepository<int, User> userRepo,
            IRepository<int, Address> addressRepo,
            IRepository<int, Cart> cartRepo,
            IRepository<int, CartItem> cartItemRepo,
            IRepository<int, Order> orderRepo,
            IRepository<int, OrderItem> orderItemRepo,
            IRepository<int, Inventory> inventoryRepo,
            IRepository<int, Payment> paymentRepo,
            IRepository<int, Refund> refundRepo,
            ILogWriter loggerDb,
            ILogger<OrderService> logger)
        {
            _userRepo = userRepo;
            _addressRepo = addressRepo;
            _cartRepo = cartRepo;
            _cartItemRepo = cartItemRepo;
            _orderRepo = orderRepo;
            _orderItemRepo = orderItemRepo;
            _inventoryRepo = inventoryRepo;
            _paymentRepo = paymentRepo;
            _refundRepo = refundRepo;
            _loggerDb = loggerDb;
            _logger = logger;
        }

        // ----------------------------------------------------------------------
        // PLACE ORDER — create Payment (Success) and sync Order.PaymentStatus
        // ----------------------------------------------------------------------
        public async Task<PlaceOrderResponseDto> PlaceOrderAsync(PlaceOrderRequestDto request, CancellationToken ct = default)
        {
            await _loggerDb.InfoAsync("OrderService.PlaceOrderAsync", "Place order started", ct: ct);

            try
            {
                // Validate user
                var userExists = await _userRepo.GetQueryable()
                    .AsNoTracking()
                    .AnyAsync(u => u.Id == request.UserId, ct);
                if (!userExists) throw new NotFoundException($"User {request.UserId} not found.");

                // Validate address for user
                var address = await _addressRepo.GetQueryable()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == request.AddressId && a.UserId == request.UserId, ct);
                if (address == null)
                    throw new BusinessValidationException("Invalid address for this user.");

                // Load cart with items + products
                var cart = await _cartRepo.GetQueryable()
                    .Include(c => c.Items)
                        .ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(c => c.UserId == request.UserId, ct);

                if (cart == null || cart.Items.Count == 0)
                    throw new BusinessValidationException("Cart is empty.");

                // Build lines from cart (prefer UnitPrice snapshot)
                var lines = cart.Items.Select(i => new
                {
                    i.ProductId,
                    i.Quantity,
                    UnitPrice = i.UnitPrice > 0 ? i.UnitPrice : (i.Product?.Price ?? 0m),
                    ProductName = i.Product?.Name ?? string.Empty,
                    SKU = i.Product?.SKU ?? string.Empty
                }).ToList();

                if (lines.Any(l => l.UnitPrice <= 0))
                    throw new BusinessValidationException("One or more products have invalid unit price.");

                // Inventory pre-check
                var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
                var invMap = await _inventoryRepo.GetQueryable()
                    .Where(inv => productIds.Contains(inv.ProductId))
                    .ToDictionaryAsync(inv => inv.ProductId, ct);

                foreach (var l in lines)
                {
                    if (!invMap.TryGetValue(l.ProductId, out var inv))
                        throw new BusinessValidationException($"Inventory missing for product {l.ProductId}.");

                    if (inv.Quantity < l.Quantity)
                        throw new BusinessValidationException($"Insufficient inventory for product {l.ProductId}. Available: {inv.Quantity}, Requested: {l.Quantity}");
                }

                // Totals
                var subTotal = lines.Sum(l => l.UnitPrice * l.Quantity);
                var shipping = request.ShippingFee ?? 0m;
                var discount = request.Discount ?? 0m;
                var total = subTotal + shipping - discount;
                if (total < 0) total = 0;

                // Create Order
                var order = new Order
                {
                    UserId = request.UserId,
                    OrderNumber = await GenerateUniqueOrderNumberAsync(ct),
                    Status = OrderStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending,
                    PlacedAtUtc = DateTime.UtcNow,

                    // Address snapshot
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

                var addedOrder = await _orderRepo.Add(order);

                // Create OrderItems & decrement inventory
                foreach (var l in lines)
                {
                    var inv = invMap[l.ProductId];
                    inv.Quantity -= l.Quantity;
                    inv.UpdatedUtc = DateTime.UtcNow;
                    await _inventoryRepo.Update(inv.Id, inv);

                    var item = new OrderItem
                    {
                        OrderId = addedOrder!.Id,
                        ProductId = l.ProductId,
                        ProductName = l.ProductName,
                        SKU = l.SKU,
                        UnitPrice = l.UnitPrice,
                        Quantity = l.Quantity,
                        LineTotal = l.UnitPrice * l.Quantity
                    };
                    await _orderItemRepo.Add(item);
                }

                // Clear cart
                foreach (var ci in cart.Items.ToList())
                    await _cartItemRepo.Delete(ci.Id);

                // Create Payment (your fields)
                var payment = new Payment
                {
                    OrderId = addedOrder!.Id,
                    UserId = request.UserId,
                    TotalAmount = total,
                    PaymentType = request.PaymentType.ToString(), // enum name as string
                    CreatedAt = DateTime.UtcNow
                };
                await _paymentRepo.Add(payment);

                // Sync Order.PaymentStatus
                addedOrder.PaymentStatus = PaymentStatus.Pending;
                addedOrder.UpdatedUtc = DateTime.UtcNow;
                await _orderRepo.Update(addedOrder.Id, addedOrder);

                await _loggerDb.InfoAsync("OrderService.PlaceOrderAsync", "Place order success", ct: ct);
                _logger.LogInformation("Order placed. OrderId={OrderId}, UserId={UserId}, Total={Total}", addedOrder.Id, request.UserId, total);

                // Response (NO payment field)
                return new PlaceOrderResponseDto
                {
                    Id = addedOrder.Id,
                    OrderNumber = addedOrder.OrderNumber,
                    Total = addedOrder.Total,
                    Status = addedOrder.Status.ToString(),
                    PaymentStatus = addedOrder.PaymentStatus.ToString(),
                    PlacedAtUtc = addedOrder.PlacedAtUtc
                };
            }
            catch (Exception ex)
            {
                await _loggerDb.ErrorAsync("OrderService.PlaceOrderAsync", "Place order failed", ex, ct: ct);
                _logger.LogError(ex, "Failed to place order for user {UserId}", request.UserId);
                throw;
            }
        }

        // ----------------------------------------------------------------------
        // GET ORDER BY ID (Include Items) — NO payment in DTO
        // ----------------------------------------------------------------------
        public async Task<OrderReadDto?> GetByIdAsync(int orderId, CancellationToken ct = default)
        {
            await _loggerDb.InfoAsync("OrderService.GetByIdAsync", "Get order by id", ct: ct);

            var order = await _orderRepo.GetQueryable()
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (order == null) return null;

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
            };
        }

        // ----------------------------------------------------------------------
        // USER ORDERS (paged) — NO payment in DTO
        // ----------------------------------------------------------------------
        public async Task<PagedResult<OrderSummaryDto>> GetUserOrdersAsync(
            int userId, int page = 1, int size = 10, string? sortBy = "date", bool desc = true, CancellationToken ct = default)
        {
            await _loggerDb.InfoAsync("OrderService.GetUserOrdersAsync", "List user orders", ct: ct);

            page = page < 1 ? 1 : page;
            size = size < 1 ? 10 : size;

            var q = _orderRepo.GetQueryable()
                .AsNoTracking()
                .Where(o => o.UserId == userId);

            q = (sortBy?.ToLowerInvariant()) switch
            {
                "total" => desc ? q.OrderByDescending(o => o.Total) : q.OrderBy(o => o.Total),
                "status" => desc ? q.OrderByDescending(o => o.Status) : q.OrderBy(o => o.Status),
                _ => desc ? q.OrderByDescending(o => o.PlacedAtUtc) : q.OrderBy(o => o.PlacedAtUtc),
            };

            var totalCount = await q.CountAsync(ct);

            var rows = await q
                .Skip((page - 1) * size)
                .Take(size)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.Status,
                    o.PlacedAtUtc,
                    o.Total,
                    ItemsCount = o.Items.Count
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

        // ----------------------------------------------------------------------
        // CANCEL ORDER — restore inventory + create Refund row (Initiated semantics via row only)
        // ----------------------------------------------------------------------
        public async Task<CancelOrderResponseDto> CancelOrderAsync(
            int orderId, int userId, bool isAdmin = false, string? reason = null, CancellationToken ct = default)
        {
            await _loggerDb.InfoAsync("OrderService.CancelOrderAsync", "Cancel order started", ct: ct);

            try
            {
                var order = await _orderRepo.GetQueryable()
                    .Include(o => o.Items)
                    .Include(o => o.Payment)
                    .FirstOrDefaultAsync(o => o.Id == orderId, ct);

                if (order == null) throw new NotFoundException("Order not found.");
                if (!isAdmin && order.UserId != userId) throw new ForbiddenException("You cannot cancel another user's order.");

                if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
                    throw new BusinessValidationException($"Order already {order.Status}, cannot cancel.");

                if (order.Status == OrderStatus.Cancelled)
                {
                    return new CancelOrderResponseDto
                    {
                        Id = order.Id,
                        Status = order.Status.ToString(),
                        Message = "Order already cancelled."
                    };
                }

                // Restore inventory
                var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
                var invMap = await _inventoryRepo.GetQueryable()
                    .Where(inv => productIds.Contains(inv.ProductId))
                    .ToDictionaryAsync(inv => inv.ProductId, ct);

                foreach (var it in order.Items)
                {
                    if (invMap.TryGetValue(it.ProductId, out var inv))
                    {
                        inv.Quantity += it.Quantity;
                        inv.UpdatedUtc = DateTime.UtcNow;
                        await _inventoryRepo.Update(inv.Id, inv);
                    }
                }

                // Update order: refund initiated → keep PaymentStatus neutral/pending
                order.Status = OrderStatus.Cancelled;
                order.PaymentStatus = PaymentStatus.Pending;
                order.UpdatedUtc = DateTime.UtcNow;
                await _orderRepo.Update(order.Id, order);

                // Create Refund row (your columns)
                if (order.Payment != null)
                {
                    var refund = new Refund
                    {
                        PaymentId = order.Payment.PaymentId,
                        OrderId = order.Id,
                        UserId = order.UserId,
                        RefundAmount = order.Total,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _refundRepo.Add(refund);
                }

                await _loggerDb.InfoAsync("OrderService.CancelOrderAsync", "Cancel order success", ct: ct);
                _logger.LogInformation("Order cancelled (refund initiated). OrderId={OrderId}, UserId={UserId}", order.Id, userId);

                return new CancelOrderResponseDto
                {
                    Id = order.Id,
                    Status = order.Status.ToString(),
                    Message = "Order cancelled. Refund initiated."
                };
            }
            catch (Exception ex)
            {
                await _loggerDb.ErrorAsync("OrderService.CancelOrderAsync", "Cancel order failed", ex, ct: ct);
                _logger.LogError(ex, "Cancel order failed. OrderId={OrderId}", orderId);
                throw;
            }
        }

        // ----------------------------------------------------------------------
        // ADMIN GET ALL (paged + filters) — NO payment in DTO
        // ----------------------------------------------------------------------
        public async Task<PagedResult<OrderReadDto>> GetAllAsync(
            string? status = null, DateTime? from = null, DateTime? to = null, int? userId = null,
            int page = 1, int size = 10, string? sortBy = "date", bool desc = true, CancellationToken ct = default)
        {
            await _loggerDb.InfoAsync("OrderService.GetAllAsync", "Admin orders query", ct: ct);

            page = page < 1 ? 1 : page;
            size = page < 1 ? 10 : size;

            var q = _orderRepo.GetQueryable().AsNoTracking();

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
                .Skip((page - 1) * size)
                .Take(size)
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

        // ----------------------------------------------------------------------
        // UPDATE STATUS (simple) — if Cancelled, reuse cancel flow
        // ----------------------------------------------------------------------
        public async Task<bool> UpdateStatusAsync(int orderId, string newStatus, CancellationToken ct = default)
        {
            await _loggerDb.InfoAsync("OrderService.UpdateStatusAsync", "Update order status", ct: ct);

            if (!Enum.TryParse<OrderStatus>(newStatus, true, out var parsed))
                throw new BusinessValidationException("Invalid order status value.");

            if (parsed == OrderStatus.Cancelled)
            {
                await CancelOrderAsync(orderId, userId: 0, isAdmin: true, reason: "Admin status update to Cancelled", ct: ct);
                return true;
            }

            var order = await _orderRepo.Get(orderId);
            if (order == null) return false;

            if (order.Status == OrderStatus.Cancelled)
                throw new ConflictException("Cancelled orders cannot change status.");

            order.Status = parsed;
            order.UpdatedUtc = DateTime.UtcNow;
            await _orderRepo.Update(orderId, order);

            await _loggerDb.InfoAsync("OrderService.UpdateStatusAsync", "Update order status success", ct: ct);
            return true;
        }

        // ----------------- helpers -----------------

        private async Task<string> GenerateUniqueOrderNumberAsync(CancellationToken ct)
        {
            string ord;
            do
            {
                var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()).Substring(0, 8);
                ord = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{token}";
            }
            while (await _orderRepo.GetQueryable()
                .AsNoTracking()
                .AnyAsync(o => o.OrderNumber == ord, ct));

            return ord;
        }
    }
}