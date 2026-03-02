namespace ShoppingWebApi.Models.DTOs.Address
{
    public class AddressUpdateDto
    {
        public string? Label { get; set; }
        public string FullName { get; set; } = default!;
        public string? Phone { get; set; }
        public string Line1 { get; set; } = default!;
        public string? Line2 { get; set; }
        public string City { get; set; } = default!;
        public string State { get; set; } = default!;
        public string PostalCode { get; set; } = default!;
        public string Country { get; set; } = "India";
    }
}