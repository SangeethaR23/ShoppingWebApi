namespace ShoppingWebApi.Models.DTOs.Orders
{
    public class OrderDetailDto
    {


        public int Id { get; set; }                // OrderItem Id
        public int ProductId { get; set; }
        public string ProductName { get; set; } = default!; // snapshot from OrderItem
        public string SKU { get; set; } = default!;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }

        public decimal LineTotal {  get; set; }

    }
}
