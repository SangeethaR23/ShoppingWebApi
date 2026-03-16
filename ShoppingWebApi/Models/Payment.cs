namespace ShoppingWebApi.Models
{
    public class Payment
    {

            public int PaymentId { get; set; }
            public int UserId { get; set; }
            public int OrderId { get; set; }
            public decimal TotalAmount { get; set; }
            public string PaymentType { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }

            // Navigation
            public User? User { get; set; }
            public Order? Order { get; set; }
            public Refund? Refund { get; set; }
        }
    

}
