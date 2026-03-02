namespace ShoppingWebApi.Models.DTOs.Orders
{
    public class CancelOrderResponseDto
    {

       public int Id { get; set; }
        public string Status { get; set; } = default!;
        public string Message { get; set; } = default!;

    }
}
