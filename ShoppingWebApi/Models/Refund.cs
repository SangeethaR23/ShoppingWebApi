namespace ShoppingWebApi.Models
{
        public class Refund
        {
            public int RefundId { get; set; }
            public int UserId { get; set; }
            public int OrderId { get; set; }
            public int PaymentId { get; set; }
            public decimal RefundAmount { get; set; }
            public DateTime CreatedAt { get; set; }

            // Navigation 
            public User? User { get; set; }
            public Payment? Payment { get; set; }
            public Order? Order { get; set; }
        }
    
}
