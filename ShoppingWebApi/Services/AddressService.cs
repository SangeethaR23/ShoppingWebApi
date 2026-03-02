using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Address;
using ShoppingWebApi.Models.DTOs.Addresses;
using ShoppingWebApi.Models.DTOs.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShoppingWebApi.Services
{
    public class AddressService : IAddressService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AddressService> _logger;

        public AddressService(AppDbContext db, ILogger<AddressService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<AddressReadDto> CreateAsync(AddressCreateDto dto, CancellationToken ct = default)
        {
            var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == dto.UserId, ct);
            if (!userExists) throw new NotFoundException($"User {dto.UserId} not found.");

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

            await _db.Addresses.AddAsync(entity, ct);
            await _db.SaveChangesAsync(ct);

            return ToReadDto(entity);
        }

        public async Task<AddressReadDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var a = await _db.Addresses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return a == null ? null : ToReadDto(a);
        }

        public async Task<PagedResult<AddressReadDto>> GetByUserAsync(int userId, int page = 1, int size = 10, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (size < 1) size = 10;

            var q = _db.Addresses.AsNoTracking().Where(a => a.UserId == userId).OrderByDescending(a => a.CreatedUtc);

            var total = await q.CountAsync(ct);
            var items = await q.Skip((page - 1) * size).Take(size)
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

        public async Task<bool> UpdateAsync(int id, int userId, AddressUpdateDto dto, CancellationToken ct = default)
        {
            var a = await _db.Addresses.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (a == null) throw new NotFoundException("Address not found.");
            if (a.UserId != userId) throw new ForbiddenException("You cannot modify another user's address.");

            a.Label = dto.Label;
            a.FullName = dto.FullName;
            a.Phone = dto.Phone;
            a.Line1 = dto.Line1;
            a.Line2 = dto.Line2;
            a.City = dto.City;
            a.State = dto.State;
            a.PostalCode = dto.PostalCode;
            a.Country = dto.Country;
            a.UpdatedUtc = System.DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> DeleteAsync(int id, int userId, CancellationToken ct = default)
        {
            var a = await _db.Addresses.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (a == null) return false;
            if (a.UserId != userId) throw new ForbiddenException("You cannot delete another user's address.");

            _db.Addresses.Remove(a);
            await _db.SaveChangesAsync(ct);
            return true;
        }

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