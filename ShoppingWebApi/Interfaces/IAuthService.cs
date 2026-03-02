using ShoppingWebApi.Models.DTOs.Auth;

namespace ShoppingWebApi.Interfaces
{
    public interface IAuthService
    {

        Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default);
        Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);

    }
}
