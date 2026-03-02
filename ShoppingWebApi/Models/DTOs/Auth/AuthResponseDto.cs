namespace ShoppingWebApi.Models.DTOs.Auth
{
    public class AuthResponseDto
    {

        public int UserId { get; set; }
        public string Email { get; set; } = default!;
        public string Role { get; set; } = "User";
        public string AccessToken { get; set; } = default!;
        public DateTime ExpiresAtUtc { get; set; }

    }
}
