namespace ShoppingWebApi.Models
{
    public class Inventory : BaseEntity
    {
        public int ProductId { get; set; } // 1:1
        public int Quantity { get; set; }
        public int ReorderLevel { get; set; } = 0;

        public Product Product { get; set; } = null!;
    }
}
