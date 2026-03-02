using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Users;
using ShoppingWebApi.Services.Security; // PasswordHasher (added during JWT step)

namespace ShoppingWebApi.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<UserService> _logger;

        public UserService(AppDbContext db, ILogger<UserService> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ---------------- Admin ----------------

        public async Task<PagedResult<UserListItemDto>> GetPagedAsync(
            string? email, string? role, string? name, string? sortBy, bool desc, int page, int size, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (size < 1) size = 10;
            sortBy ??= "date";

            var q = _db.Users.AsNoTracking()
                .Include(u => u.UserDetails)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
                q = q.Where(u => u.Email.Contains(email));
            if (!string.IsNullOrWhiteSpace(role))
                q = q.Where(u => (u.Role ?? "User") == role);
            if (!string.IsNullOrWhiteSpace(name))
                q = q.Where(u =>
                    (u.UserDetails != null && (
                        u.UserDetails.FirstName.Contains(name) ||
                        u.UserDetails.LastName.Contains(name))));

            q = sortBy.ToLowerInvariant() switch
            {
                "email" => desc ? q.OrderByDescending(u => u.Email) : q.OrderBy(u => u.Email),
                "name" => desc ? q.OrderByDescending(u => u.UserDetails!.FirstName).ThenByDescending(u => u.UserDetails!.LastName)
                                : q.OrderBy(u => u.UserDetails!.FirstName).ThenBy(u => u.UserDetails!.LastName),
                "role" => desc ? q.OrderByDescending(u => u.Role) : q.OrderBy(u => u.Role),
                _ => desc ? q.OrderByDescending(u => u.CreatedUtc) : q.OrderBy(u => u.CreatedUtc),
            };

            var total = await q.CountAsync(ct);

            var rows = await q.Skip((page - 1) * size).Take(size)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Role = u.Role ?? "User",
                    FullName = u.UserDetails == null ? string.Empty : (u.UserDetails.FirstName + " " + u.UserDetails.LastName).Trim(),
                    Phone = u.UserDetails!.Phone,
                    CreatedUtc = u.CreatedUtc
                })
                .ToListAsync(ct);

            return new PagedResult<UserListItemDto>
            {
                Items = rows,
                TotalCount = total,
                PageNumber = page,
                PageSize = size
            };
        }

        public async Task<UserProfileReadDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var u = await _db.Users.AsNoTracking()
                .Include(u => u.UserDetails)
                .FirstOrDefaultAsync(u => u.Id == id, ct);

            return u == null ? null : ToProfileDto(u);
        }

        public async Task<bool> UpdateRoleAsync(int id, string role, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(role)) throw new BusinessValidationException("Role cannot be empty.");
            role = role.Trim();
            if (role != "Admin" && role != "User")
                throw new BusinessValidationException("Role must be 'Admin' or 'User'.");

            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (u == null) throw new NotFoundException("User not found.");

            u.Role = role;
            u.UpdatedUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---------------- User ----------------

        public async Task<UserProfileReadDto?> GetProfileAsync(int userId, CancellationToken ct = default)
        {
            var u = await _db.Users.AsNoTracking()
                .Include(u => u.UserDetails)
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            return u == null ? null : ToProfileDto(u);
        }

        public async Task<UserProfileReadDto> UpdateProfileAsync(int userId, UpdateUserProfileDto dto, CancellationToken ct = default)
        {
            var u = await _db.Users
                .Include(x => x.UserDetails)
                .FirstOrDefaultAsync(x => x.Id == userId, ct);

            if (u == null) throw new NotFoundException("User not found.");

            if (u.UserDetails == null)
            {
                // Create details row to satisfy [Required] FirstName/LastName
                u.UserDetails = new UserDetails
                {
                    UserId = userId,
                    FirstName = string.IsNullOrWhiteSpace(dto.FirstName) ? " " : dto.FirstName!.Trim(),
                    LastName = string.IsNullOrWhiteSpace(dto.LastName) ? " " : dto.LastName!.Trim(),
                    Phone = dto.Phone,
                    DateOfBirth = dto.DateOfBirth
                };
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                if (dto.FirstName != null) u.UserDetails.FirstName = dto.FirstName.Trim();
                if (dto.LastName != null) u.UserDetails.LastName = dto.LastName.Trim();
                if (dto.Phone != null) u.UserDetails.Phone = dto.Phone;
                if (dto.DateOfBirth.HasValue) u.UserDetails.DateOfBirth = dto.DateOfBirth;

                u.UserDetails.User = u; // keep consistency
                u.UserDetails.UserId = u.Id;
                u.UpdatedUtc = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);
            }

            // Ensure non-empty first/last after update (entity has [Required])
            if (string.IsNullOrWhiteSpace(u.UserDetails.FirstName) || string.IsNullOrWhiteSpace(u.UserDetails.LastName))
                throw new BusinessValidationException("FirstName and LastName cannot be empty.");

            return ToProfileDto(u);
        }

        public async Task<bool> ChangePasswordAsync(ChangePasswordRequestDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                throw new BusinessValidationException("New password must be at least 6 characters.");

            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == dto.UserId, ct);
            if (u == null) throw new NotFoundException("User not found.");

            // Verify current password
            if (!PasswordHasher.Verify(dto.CurrentPassword, u.PasswordHash))
                throw new UnauthorizedAppException("Current password is incorrect.");

            u.PasswordHash = PasswordHasher.Hash(dto.NewPassword);
            u.UpdatedUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---------------- Helpers ----------------

        private static UserProfileReadDto ToProfileDto(User u) => new UserProfileReadDto
        {
            Id = u.Id,
            Email = u.Email,
            Role = u.Role ?? "User",
            FirstName = u.UserDetails?.FirstName ?? string.Empty,
            LastName = u.UserDetails?.LastName ?? string.Empty,
            Phone = u.UserDetails?.Phone,
            DateOfBirth = u.UserDetails?.DateOfBirth,
            CreatedUtc = u.CreatedUtc,
            UpdatedUtc = u.UpdatedUtc
        };
    }
}