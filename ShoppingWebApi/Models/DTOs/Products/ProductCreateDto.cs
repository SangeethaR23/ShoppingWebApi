using System.ComponentModel.DataAnnotations;

namespace ShoppingWebApi.Models.DTOs.Products
{
    public class ProductCreateDto
    {
        public string Name { get; set; } = null!;
        public string SKU { get; set; } = null!;
        public decimal Price { get; set; }
        public int CategoryId { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class ProductUpdateDto : ProductCreateDto
    {
        public int Id { get; set; }
    }

    public class ProductImageCreateDto
    {
        [Required, Url, MaxLength(2000)]
        public string Url { get; set; } = null!;
    }

}
