using System.ComponentModel.DataAnnotations;

namespace ShoppingWebApi.Models
{
    public class Address : BaseEntity
    {
        public int UserId { get; set; }

        [MaxLength(100)]
        public string? Label { get; set; } // optional: "Home", "Office"

        [Required, MaxLength(120)]
        public string FullName { get; set; } = null!;

        [MaxLength(20)]
        public string? Phone { get; set; }

        [Required, MaxLength(200)]
        public string Line1 { get; set; } = null!;

        [MaxLength(200)]
        public string? Line2 { get; set; }

        [Required, MaxLength(100)]
        public string City { get; set; } = null!;

        [Required, MaxLength(100)]
        public string State { get; set; } = null!;

        [Required, MaxLength(20)]
        public string PostalCode { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Country { get; set; } = "India";

        public User User { get; set; } = null!;
    }
}