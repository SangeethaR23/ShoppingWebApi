namespace ShoppingWebApi.Models.DTOs.Products
{
    public class ProductReadDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string SKU { get; set; } = null!;
        public decimal Price { get; set; }
        public int CategoryId { get; set; }
        public bool IsActive { get; set; }

        // Summary fields (computed in service)
        public double AverageRating { get; set; }
        public int ReviewsCount { get; set; }

        public List<ProductImageReadDto> Images { get; set; } = new();
    }

    public class ProductImageReadDto
    {
        public int Id { get; set; }
        public string Url { get; set; } = null!;
    }
}
