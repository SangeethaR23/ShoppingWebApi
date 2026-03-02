namespace ShoppingWebApi.Models
{
    public class OrderItem : BaseEntity
    {
        public int OrderId { get; set; }
        public int ProductId { get; set; }

        // snapshot fields (preserve history)
        public string ProductName { get; set; } = null!;
        public string SKU { get; set; } = null!;
        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }

        public Order Order { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }
}
