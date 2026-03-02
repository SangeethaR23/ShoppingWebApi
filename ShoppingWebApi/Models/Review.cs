using System.ComponentModel.DataAnnotations;

namespace ShoppingWebApi.Models
{
    public class Review : BaseEntity
    {
        public int ProductId { get; set; }
        public int UserId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(2000)]
        public string? Comment { get; set; }

        public Product Product { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}