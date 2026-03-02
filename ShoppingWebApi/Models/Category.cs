using System.ComponentModel.DataAnnotations;

namespace ShoppingWebApi.Models
{
    public class Category : BaseEntity
    {
        [Required, MaxLength(120)]
        public string Name { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        public int? ParentCategoryId { get; set; }
        public Category? ParentCategory { get; set; }
        public ICollection<Category> Children { get; set; } = new List<Category>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}