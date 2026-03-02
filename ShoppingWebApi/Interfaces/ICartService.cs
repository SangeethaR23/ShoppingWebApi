using ShoppingWebApi.Models.DTOs.Cart;

namespace ShoppingWebApi.Interfaces
{
    public interface ICartService
    {
        Task<CartReadDto> GetByUserIdAsync(int userId, CancellationToken ct = default);

        Task<CartReadDto> AddItemAsync(int userId, CartAddItemDto dto, CancellationToken ct = default);

        Task<CartReadDto> UpdateItemAsync(int userId, CartUpdateItemDto dto, CancellationToken ct = default);

        Task RemoveItemAsync(int userId, int productId, CancellationToken ct = default);

        Task ClearAsync(int userId, CancellationToken ct = default);
    }
}