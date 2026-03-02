using System;

namespace ShoppingWebApi.Models.DTOs.Users
{
    public class UserProfileReadDto
    {
        public int Id { get; set; }              // User Id
        public string Email { get; set; } = default!;
        public string Role { get; set; } = "User";

        // Details
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public DateTime? DateOfBirth { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
    }
}