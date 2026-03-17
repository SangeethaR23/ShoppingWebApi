using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Address;
using ShoppingWebApi.Models.DTOs.Addresses;
using ShoppingWebApi.Models.DTOs.Common;

namespace ShoppingWebApi.Services
{
    public class AddressService : IAddressService
    {
        private readonly IRepository<int, Address> _addressRepo;
        private readonly IRepository<int, User> _userRepo;
        private readonly ILogger<AddressService> _logger;

        public AddressService(
            IRepository<int, Address> addressRepo,
            IRepository<int, User> userRepo,
            ILogger<AddressService> logger)
        {
            _addressRepo = addressRepo;
            _userRepo = userRepo;
            _logger = logger;
        }

        // ------------------------------------------------------------
        // CREATE (repo)
        // ------------------------------------------------------------
        public async Task<AddressReadDto> CreateAsync(AddressCreateDto dto, CancellationToken ct = default)
        {
            var user = await _userRepo.Get(dto.UserId);
            if (user is null)
                throw new NotFoundException($"User {dto.UserId} not found.");

            var entity = new Address
            {
                UserId = dto.UserId,
                Label = dto.Label,
                FullName = dto.FullName,
                Phone = dto.Phone,
                Line1 = dto.Line1,
                Line2 = dto.Line2,
                City = dto.City,
                State = dto.State,
                PostalCode = dto.PostalCode,
                Country = dto.Country
            };

            var added = await _addressRepo.Add(entity);
            return ToReadDto(added!);
        }

        // ------------------------------------------------------------
        // GET BY ID (repo.GetQueryable)
        // ------------------------------------------------------------
        public async Task<AddressReadDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var a = await _addressRepo.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            return a == null ? null : ToReadDto(a);
        }

        // ------------------------------------------------------------
        // GET USER ADDRESSES (IQueryable + paging)
        // ------------------------------------------------------------
        public async Task<PagedResult<AddressReadDto>> GetByUserAsync(
            int userId, int page = 1, int size = 10, CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            size = Math.Max(1, size);

            var q = _addressRepo.GetQueryable()
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedUtc);

            var total = await q.CountAsync(ct);

            var items = await q.Skip((page - 1) * size)
                .Take(size)
                .Select(a => ToReadDto(a))
                .ToListAsync(ct);

            return new PagedResult<AddressReadDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = size
            };
        }

        // ------------------------------------------------------------
        // UPDATE (repo CRUD + IQueryable validation)
        // ------------------------------------------------------------
        public async Task<bool> UpdateAsync(int id, int userId, AddressUpdateDto dto, CancellationToken ct = default)
        {
            var address = await _addressRepo.Get(id);
            if (address is null)
                throw new NotFoundException("Address not found.");

            if (address.UserId != userId)
                throw new ForbiddenException("You cannot modify another user's address.");

            address.Label = dto.Label;
            address.FullName = dto.FullName;
            address.Phone = dto.Phone;
            address.Line1 = dto.Line1;
            address.Line2 = dto.Line2;
            address.City = dto.City;
            address.State = dto.State;
            address.PostalCode = dto.PostalCode;
            address.Country = dto.Country;
            address.UpdatedUtc = DateTime.UtcNow;

            await _addressRepo.Update(id, address);
            return true;
        }

        // ------------------------------------------------------------
        // DELETE (repo)
        // ------------------------------------------------------------
        public async Task<bool> DeleteAsync(int id, int userId, CancellationToken ct = default)
        {
            var address = await _addressRepo.Get(id);
            if (address == null)
                return false;

            if (address.UserId != userId)
                throw new ForbiddenException("You cannot delete another user's address.");

            await _addressRepo.Delete(id);
            return true;
        }

        // ------------------------------------------------------------
        // MAPPER
        // ------------------------------------------------------------
        private static AddressReadDto ToReadDto(Address a) => new AddressReadDto
        {
            Id = a.Id,
            UserId = a.UserId,
            Label = a.Label,
            FullName = a.FullName,
            Phone = a.Phone,
            Line1 = a.Line1,
            Line2 = a.Line2,
            City = a.City,
            State = a.State,
            PostalCode = a.PostalCode,
            Country = a.Country,
            CreatedUtc = a.CreatedUtc,
            UpdatedUtc = a.UpdatedUtc
        };
    }
}