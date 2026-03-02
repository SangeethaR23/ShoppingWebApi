using ShoppingWebApi.Models.DTOs.Address;
using ShoppingWebApi.Models.DTOs.Addresses;
using ShoppingWebApi.Models.DTOs.Common;


namespace ShoppingWebApi.Interfaces
{
    public interface IAddressService
    {
        Task<AddressReadDto> CreateAsync(AddressCreateDto dto, CancellationToken ct = default);
        Task<AddressReadDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<PagedResult<AddressReadDto>> GetByUserAsync(int userId, int page = 1, int size = 10, CancellationToken ct = default);
        Task<bool> UpdateAsync(int id, int userId, AddressUpdateDto dto, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, int userId, CancellationToken ct = default);
    }
}