namespace ShoppingWebApi.Models.DTOs.Reviews
{
    public class ReviewCreateDto
    {
        public int UserId { get; set; }      // replace with JWT later
        public int ProductId { get; set; }
        public int Rating { get; set; }      // 1..5
        public string? Comment { get; set; }
    }
}