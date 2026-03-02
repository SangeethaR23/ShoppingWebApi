using System;

namespace ShoppingWebApi.Models.DTOs.Orders
{
    public class PlaceOrderResponseDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = default!;
        public decimal Total { get; set; }
        public string Status { get; set; } = default!;         // e.g., "Pending"
        public string PaymentStatus { get; set; } = default!;  // e.g., "Pending"
        public DateTime PlacedAtUtc { get; set; }
    }
}