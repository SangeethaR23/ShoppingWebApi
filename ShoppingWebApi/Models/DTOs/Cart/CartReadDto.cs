using System.ComponentModel.DataAnnotations;

namespace ShoppingWebApi.Models.DTOs.Cart
{
    public class CartReadDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public List<CartItemReadDto> Items { get; set; } = new();
        public decimal SubTotal { get; set; }
    }

    public class CartItemReadDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string SKU { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }

        // For “check reviews in cart”
        public double AverageRating { get; set; }
        public int ReviewsCount { get; set; }
    }

    public class CartAddItemDto
    {
        [Required] public int ProductId { get; set; }
        [Range(1, int.MaxValue)] public int Quantity { get; set; } = 1;
    }

    public class CartUpdateItemDto
    {
        [Required] public int ProductId { get; set; }
        [Range(0, int.MaxValue)] public int Quantity { get; set; } // 0 => remove
    }


}
