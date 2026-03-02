using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Auth;
using ShoppingWebApi.Services.Security;

namespace ShoppingWebApi.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(AppDbContext db, ITokenService tokenService, ILogger<AuthService> logger)
        {
            _db = db;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default)
        {
            var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email, ct);
            if (exists) throw new ConflictException("Email already registered.");

            var user = new User
            {
                Email = dto.Email.Trim(),
                PasswordHash = PasswordHasher.Hash(dto.Password),
                Role = string.IsNullOrWhiteSpace(dto.Role) ? "User" : dto.Role!.Trim()
            };

            await _db.Users.AddAsync(user, ct);
            await _db.SaveChangesAsync(ct);

            // Optionally create UserDetails if provided
            if (!string.IsNullOrWhiteSpace(dto.FirstName) || !string.IsNullOrWhiteSpace(dto.LastName) || !string.IsNullOrWhiteSpace(dto.Phone))
            {
                var details = new UserDetails
                {
                    UserId = user.Id,
                    FirstName = dto.FirstName ?? string.Empty,
                    LastName = dto.LastName ?? string.Empty,
                    Phone = dto.Phone
                };
                await _db.UserDetails.AddAsync(details, ct);
                await _db.SaveChangesAsync(ct);
            }

            var claims = BuildClaims(user);
            var token = _tokenService.CreateAccessToken(claims, out var exp);

            return new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role ?? "User",
                AccessToken = token,
                ExpiresAtUtc = exp
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email, ct);
            if (user == null) throw new UnauthorizedAppException("Invalid credentials.");

            if (!PasswordHasher.Verify(dto.Password, user.PasswordHash))
                throw new UnauthorizedAppException("Invalid credentials.");

            var claims = BuildClaims(user);
            var token = _tokenService.CreateAccessToken(claims, out var exp);

            return new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role ?? "User",
                AccessToken = token,
                ExpiresAtUtc = exp
            };
        }

        private static Claim[] BuildClaims(User user)
        {
            return new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "User"),
                new Claim("userId", user.Id.ToString()) // convenience for APIs
            };
        }
    }
}