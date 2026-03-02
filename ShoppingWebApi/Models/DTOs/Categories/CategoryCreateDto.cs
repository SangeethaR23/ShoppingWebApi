using System.ComponentModel.DataAnnotations;

namespace ShoppingWebApi.Models.DTOs.Categories
{
    public class CategoryCreateDto
    {
        [Required, MaxLength(120)]
        public string Name { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        public int? ParentCategoryId { get; set; }
    }

    public class CategoryUpdateDto : CategoryCreateDto
    {
        [Required]
        public int Id { get; set; }
    }

    public class CategoryReadDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int? ParentCategoryId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
    }
}
