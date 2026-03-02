
namespace ShoppingWebApi.Models.DTOs.Addresses
{
    public class AddressReadDto
    {
        public int Id { get; set; }
        public string? Label { get; set; }
        public string FullName { get; set; } = null!;
        public string? Phone { get; set; }
        public string Line1 { get; set; } = null!;
        public string? Line2 { get; set; }
        public string City { get; set; } = null!;
        public string State { get; set; } = null!;
        public string PostalCode { get; set; } = null!;
        public string Country { get; set; } = null!;
        public int UserId { get; internal set; }
        public DateTime CreatedUtc { get; internal set; }
        public DateTime? UpdatedUtc { get; internal set; }
    }
}
