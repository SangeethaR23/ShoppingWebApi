namespace ShoppingWebApi.Models.DTOs.Users
{
    public class UserListItemDto
    {


        public int Id { get; set; }
        public string Email { get; set; } = default!;
        public string Role { get; set; } = "User";
        public string FullName { get; set; } = string.Empty;  // FirstName + LastName
        public string? Phone { get; set; }
        public DateTime CreatedUtc { get; set; }

    }
}
