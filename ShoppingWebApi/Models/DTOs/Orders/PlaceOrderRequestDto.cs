using System.ComponentModel.DataAnnotations;

namespace ShoppingWebApi.Models.DTOs.Orders
{
    public class PlaceOrderRequestDto
    {
        public int UserId { get; set; }
        public int AddressId { get; set; }
        public string? Notes { get; set; }
        public decimal? ShippingFee { get; internal set; }
        public decimal? Discount { get; internal set; }

        [Required(ErrorMessage = "Payment type is required")]
        public string PaymentType { get; set; } = string.Empty;
    }
}
