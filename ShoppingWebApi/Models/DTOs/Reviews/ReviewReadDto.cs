using System.ComponentModel.DataAnnotations;

namespace ShoppingWebApi.Models.DTOs.Reviews
{
    public class ReviewReadDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UserId { get; set; }

        public int Rating { get; set; }         // 1..5
        public string? Comment { get; set; }    // up to 2000

        // Extra info for UI convenience (optional)
        public string? UserName { get; set; }   // from UserDetails (FirstName + LastName) or Email
        public DateTime CreatedUtc { get; set; }
    }
}

  
    