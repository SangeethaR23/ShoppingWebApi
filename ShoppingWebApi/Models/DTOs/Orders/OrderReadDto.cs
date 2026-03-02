using System;
using System.Collections.Generic;

namespace ShoppingWebApi.Models.DTOs.Orders
{
    public class OrderReadDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = default!;
        public string Status { get; set; } = default!;
        public string PaymentStatus { get; set; } = default!;
        public DateTime PlacedAtUtc { get; set; }

        // Shipping snapshot
        public string ShipToName { get; set; } = default!;
        public string? ShipToPhone { get; set; }
        public string ShipToLine1 { get; set; } = default!;
        public string? ShipToLine2 { get; set; }
        public string ShipToCity { get; set; } = default!;
        public string ShipToState { get; set; } = default!;
        public string ShipToPostalCode { get; set; } = default!;
        public string ShipToCountry { get; set; } = default!;

        // Money
        public decimal SubTotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }

        public List<OrderDetailDto> Items { get; set; } = new();
    }
}