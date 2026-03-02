namespace ShoppingWebApi.Models
{
    public class CartItem : BaseEntity
    {
        public int CartId { get; set; }
        public int ProductId { get; set; }

        public int Quantity { get; set; }

        // snapshot price when added (we can decide recalc later)
        public decimal UnitPrice { get; set; }

        public Cart Cart { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }
}