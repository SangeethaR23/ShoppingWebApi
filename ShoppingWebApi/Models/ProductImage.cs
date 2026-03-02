using System.ComponentModel.DataAnnotations;

namespace ShoppingWebApi.Models
{
    public class ProductImage : BaseEntity
    {
        public int ProductId { get; set; }

        [Required, MaxLength(2000)]
        public string Url { get; set; } = null!;

        public Product Product { get; set; } = null!;
    }
}