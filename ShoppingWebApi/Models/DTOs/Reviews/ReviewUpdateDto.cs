namespace ShoppingWebApi.Models.DTOs.Reviews
{
    public class ReviewUpdateDto
    {

        public int Rating { get; set; }      // 1..5
        public string? Comment { get; set; }

    }
}
