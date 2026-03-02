using System;

namespace ShoppingWebApi.Models.DTOs.Inventory
{
    public class InventoryReadDto
    {
        public int Id { get; set; }                // Inventory Id
        public int ProductId { get; set; }
        public string ProductName { get; set; } = default!;
        public string SKU { get; set; } = default!;
        public int Quantity { get; set; }
        public int ReorderLevel { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
    }
}