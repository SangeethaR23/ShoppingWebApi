using System;
using System.Threading;
using System.Threading.Tasks;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Orders;

namespace ShoppingWebApi.Interfaces
{
    public interface IOrderService
    {
        Task<PlaceOrderResponseDto> PlaceOrderAsync(PlaceOrderRequestDto request, CancellationToken ct = default);

        Task<OrderReadDto?> GetByIdAsync(int orderId, CancellationToken ct = default);

        Task<PagedResult<OrderSummaryDto>> GetUserOrdersAsync(
            int userId, int page = 1, int size = 10, string? sortBy = "date", bool desc = true, CancellationToken ct = default);

        Task<CancelOrderResponseDto> CancelOrderAsync(
            int orderId, int userId, bool isAdmin = false, string? reason = null, CancellationToken ct = default);

        Task<PagedResult<OrderReadDto>> GetAllAsync(
            string? status = null, DateTime? from = null, DateTime? to = null, int? userId = null,
            int page = 1, int size = 10, string? sortBy = "date", bool desc = true, CancellationToken ct = default);

        Task<bool> UpdateStatusAsync(int orderId, string newStatus, CancellationToken ct = default);
    }
}