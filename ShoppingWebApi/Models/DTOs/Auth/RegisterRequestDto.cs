namespace ShoppingWebApi.Models.DTOs.Auth
{
    public class RegisterRequestDto
    {

        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? Role { get; set; }

    }
}
