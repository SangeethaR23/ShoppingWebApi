using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShoppingWebApi.Common; // <-- for User.GetUserId()
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models.DTOs.Address;
using ShoppingWebApi.Models.DTOs.Addresses;
using ShoppingWebApi.Models.DTOs.Common;

namespace ShoppingWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AddressesController : ControllerBase
    {
        private readonly IAddressService _service;

        public AddressesController(IAddressService service)
        {
            _service = service;
        }

        /// <summary>Create a new address for the signed-in user.</summary>
        [Authorize]
        [HttpPost]
        [ProducesResponseType(typeof(AddressReadDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] AddressCreateDto dto, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            // Enforce identity from JWT
            dto.UserId = userId.Value;

            var res = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = res.Id }, res);
        }

        /// <summary>Get a specific address by id (owner or admin only).</summary>
        [Authorize]
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(AddressReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            if (dto == null) return NotFound();

            var userId = User.GetUserId();
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && (userId is null || dto.UserId != userId.Value))
                return Forbid();

            return Ok(dto);
        }

        /// <summary>Get current user's addresses (paged).</summary>
        [Authorize]
        [HttpGet("mine")]
        [ProducesResponseType(typeof(PagedResult<AddressReadDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMine([FromQuery] int page = 1, [FromQuery] int size = 10, CancellationToken ct = default)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var res = await _service.GetByUserAsync(userId.Value, page, size, ct);
            return Ok(res);
        }

        /// <summary>Admin: list addresses of a user (paged).</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpGet("user/{userId:int}")]
        [ProducesResponseType(typeof(PagedResult<AddressReadDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetByUser([FromRoute] int userId, [FromQuery] int page = 1, [FromQuery] int size = 10, CancellationToken ct = default)
        {
            var res = await _service.GetByUserAsync(userId, page, size, ct);
            return Ok(res);
        }

        /// <summary>Update my address.</summary>
        [Authorize]
        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] AddressUpdateDto dto, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            await _service.UpdateAsync(id, userId.Value, dto, ct);
            return Ok(new {message="Address Updated successfully"});
        }

        /// <summary>Delete my address.</summary>
        [Authorize]
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var ok = await _service.DeleteAsync(id, userId.Value, ct);
            return ok ? Ok(new {message="Address deleted."}) : NotFound();
        }
    }
}