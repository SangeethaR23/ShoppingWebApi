using System.ComponentModel.DataAnnotations;

namespace ShoppingWebApi.Models
{
    public class Order : BaseEntity
    {
        public int UserId { get; set; }

        [Required, MaxLength(40)]
        public string OrderNumber { get; set; } = null!; // unique

        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public DateTime PlacedAtUtc { get; set; } = DateTime.UtcNow;

        // Address snapshot for shipping
        [Required, MaxLength(120)]
        public string ShipToName { get; set; } = null!;

        [MaxLength(20)]
        public string? ShipToPhone { get; set; }

        [Required, MaxLength(200)]
        public string ShipToLine1 { get; set; } = null!;

        [MaxLength(200)]
        public string? ShipToLine2 { get; set; }

        [Required, MaxLength(100)]
        public string ShipToCity { get; set; } = null!;

        [Required, MaxLength(100)]
        public string ShipToState { get; set; } = null!;

        [Required, MaxLength(20)]
        public string ShipToPostalCode { get; set; } = null!;

        [Required, MaxLength(100)]
        public string ShipToCountry { get; set; } = "India";

        public decimal SubTotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }

        public User User { get; set; } = null!;//one to many
        public Payment? Payment { get; set; } // one to one 

        public Refund? Refund { get; set; }

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}