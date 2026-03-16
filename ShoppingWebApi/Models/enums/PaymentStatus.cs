namespace ShoppingWebApi.Models.enums
{
    
    namespace ShoppingWebApi.Models.enums
    {
        // How the user paid
        public enum PaymentMethod
        {
            Unknown = 0,
            CashOnDelivery = 1, // COD
            UPI = 2,
            Card = 3,          // Credit/Debit
            NetBanking = 4,
            Wallet = 5
        }

        // State of payment *from the order perspective*
        public enum PaymentStatus
        {
            Pending = 0,
            Success = 1,
            Failed = 2
        }

        // State of refund processing (useful even if Refund table doesn't have a Status column yet)
        public enum RefundStatus
        {
            Initiated = 0,
            Success = 1,
            Failed = 2
        }
    }
}
