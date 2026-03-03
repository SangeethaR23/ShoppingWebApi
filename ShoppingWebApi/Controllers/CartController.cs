using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShoppingWebApi.Common; // for User.GetUserId()
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models.DTOs.Cart;

namespace ShoppingWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly ICartService _service;

        public CartController(ICartService service)
        {
            _service = service;
        }

        // ============================
        // AUTHENTICATED USER ("me")
        // ============================

        /// <summary>Get my cart (resolved from JWT).</summary>
        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType(typeof(CartReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<CartReadDto>> GetMyCart(CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var dto = await _service.GetByUserIdAsync(userId.Value, ct);
            return Ok(dto);
        }

        /// <summary>Add an item to my cart (merge if same product).</summary>
        [Authorize]
        [HttpPost("items")]
        [ProducesResponseType(typeof(CartReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<CartReadDto>> AddItemMe([FromBody] CartAddItemDto dto, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var result = await _service.AddItemAsync(userId.Value, dto, ct);
            return Ok(result);
        }

        /// <summary>Update an item in my cart (e.g., change quantity).</summary>
        [Authorize]
        [HttpPut("items")]
        [ProducesResponseType(typeof(CartReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<CartReadDto>> UpdateItemMe([FromBody] CartUpdateItemDto dto, CancellationToken ct)
        {
            var userId = User.GetUserId();
           
            if (userId is null) return Unauthorized();
            try
            {

                var result = await _service.UpdateItemAsync(userId.Value, dto, ct);
                return Ok(result);
            }catch(NotFoundException)
            {
                return BadRequest(new { message = "Cart item not found" });
            }
        }

        /// <summary>Remove an item from my cart by productId.</summary>
        [Authorize]
        [HttpDelete("items/{productId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RemoveItemMe([FromRoute] int productId, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();
            try
            {

                await _service.RemoveItemAsync(userId.Value, productId, ct);
                return Ok(new { message = "Item removed successfully" });
            }catch(NotFoundException)
            {
                return BadRequest(new { message = "Cart item not found" });
            }
        }

        /// <summary>Clear my entire cart.</summary>
        [Authorize]
        [HttpDelete("items")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ClearMe(CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            await _service.ClearAsync(userId.Value, ct);
            return Ok(new {message="There is no item in the cart"});
        }

        // ===========================================
        // ADMIN/SERVER-SIDE VARIANTS (by userId)
        // ===========================================

        /// <summary>Admin: Get cart for a user.</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpGet("by-user/{userId:int}")]
        [ProducesResponseType(typeof(CartReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CartReadDto>> GetByUserId(int userId, CancellationToken ct)
        {
            var dto = await _service.GetByUserIdAsync(userId, ct);
            return Ok(dto);
        }

        /// <summary>Admin: Add an item to a user’s cart.</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpPost("by-user/{userId:int}/items")]
        [ProducesResponseType(typeof(CartReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CartReadDto>> AddItem(int userId, [FromBody] CartAddItemDto dto, CancellationToken ct)
        {
            var result = await _service.AddItemAsync(userId, dto, ct);
            return Ok(result);
        }

        /// <summary>Admin: Update an item in a user’s cart.</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpPut("by-user/{userId:int}/items")]
        [ProducesResponseType(typeof(CartReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CartReadDto>> UpdateItem(int userId, [FromBody] CartUpdateItemDto dto, CancellationToken ct)
        {
            var result = await _service.UpdateItemAsync(userId, dto, ct);
            return Ok(result);
        }

       //Item remove by itemID
        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("by-user/{userId:int}/items/{productId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RemoveItem(int userId, int productId, CancellationToken ct)
        {
            await _service.RemoveItemAsync(userId, productId, ct);
            return Ok(new {message="Product removed Successfully"});
        }

        //remove all items
        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("by-user/{userId:int}/items")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Clear(int userId, CancellationToken ct)
        {
            await _service.ClearAsync(userId, ct);
            return Ok(new {message="Cleared Successfully"});
        }
    }
}