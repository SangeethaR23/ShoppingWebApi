using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShoppingWebApi.Common; // for User.GetUserId()
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Reviews;

namespace ShoppingWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _service;

        public ReviewsController(IReviewService service)
        {
            _service = service;
        }

        /// <summary>Create a review for a product (1 per user per product).</summary>
        [Authorize]
        [HttpPost]
        [ProducesResponseType(typeof(ReviewReadDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] ReviewCreateDto dto, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            // Enforce user identity from JWT
            dto.UserId = userId.Value;

            var res = await _service.CreateAsync(dto, ct);
            // "mine" derives user from claims, so only productId is required
            return CreatedAtAction(nameof(GetMineForProduct), new { productId = res.ProductId }, res);
        }

        /// <summary>List reviews for a product (public).</summary>
        [HttpGet("product/{productId:int}")]
        [ProducesResponseType(typeof(PagedResult<ReviewReadDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByProduct([FromRoute] int productId, [FromQuery] int page = 1, [FromQuery] int size = 10, CancellationToken ct = default)
        {
            var res = await _service.GetByProductAsync(productId, page, size, ct);
            return Ok(res);
        }

        /// <summary>Get my review for a product (if any).</summary>
        [Authorize]
        [HttpGet("product/{productId:int}/mine")]
        [ProducesResponseType(typeof(ReviewReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMineForProduct([FromRoute] int productId, CancellationToken ct = default)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var dto = await _service.GetAsync(productId, userId.Value, ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        /// <summary>Update my review for a product.</summary>
        [Authorize]
        [HttpPut("product/{productId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update([FromRoute] int productId, [FromBody] ReviewUpdateDto dto, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            await _service.UpdateAsync(productId, userId.Value, dto, ct);
            return Ok(new {message="Review Updated Successfully"});
        }

        /// <summary>Delete my review for a product.</summary>
        [Authorize]
        [HttpDelete("product/{productId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete([FromRoute] int productId, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var ok = await _service.DeleteAsync(productId, userId.Value, ct);
            return ok ? Ok(new {message="Review Deleted Successfully"}) : NotFound();
        }
    }
}