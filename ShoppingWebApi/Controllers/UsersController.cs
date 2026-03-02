using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShoppingWebApi.Common; 
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Users;

namespace ShoppingWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;

        public UsersController(IUserService service)
        {
            _service = service;
        }

        /// <summary>Admin: paged user list with filters.</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<UserListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPaged(
            [FromQuery] string? email,
            [FromQuery] string? role,
            [FromQuery] string? name,
            [FromQuery] string? sortBy = "date",
            [FromQuery] bool desc = true,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            CancellationToken ct = default)
        {
            var res = await _service.GetPagedAsync(email, role, name, sortBy, desc, page, size, ct);
            return Ok(res);
        }

        /// <summary>Admin: get any user by id.</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(UserProfileReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        /// <summary>Admin: update role (Admin/User).</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpPatch("{id:int}/role")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateRole([FromRoute] int id, [FromQuery] string role, CancellationToken ct)
        {
            await _service.UpdateRoleAsync(id, role, ct);
            return Ok(new {message="Role Updated Successfully"});
        }

        /// <summary>User: get my profile.</summary>
        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserProfileReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMe(CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var dto = await _service.GetProfileAsync(userId.Value, ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        /// <summary>User: update my profile.</summary>
        [Authorize]
        [HttpPut("me")]
        [ProducesResponseType(typeof(UserProfileReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateUserProfileDto dto, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var res = await _service.UpdateProfileAsync(userId.Value, dto, ct);
            return Ok(res);
        }

        /// <summary>User: change my password.</summary>
        [Authorize]
        [HttpPost("me/change-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto dto, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            // Enforce identity from token (ignore any userId from client if present)
            dto.UserId = userId.Value;

            await _service.ChangePasswordAsync(dto, ct);
            return Ok(new {message="Password changed successfully."});
        }
    }
}