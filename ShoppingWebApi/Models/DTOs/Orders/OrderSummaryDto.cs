using System;

namespace ShoppingWebApi.Models.DTOs.Orders
{
    public class OrderSummaryDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateTime PlacedAtUtc { get; set; }
        public decimal Total { get; set; }
        public int ItemsCount { get; set; }
    }
}