using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShoppingWebApi.Common;                  // <-- add this (claims helper)
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Orders;

namespace ShoppingWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _service;

        public OrdersController(IOrderService service)
        {
            _service = service;
        }

        /// <summary>Place an order using the user's cart and selected address.</summary>
        [Authorize]
        [HttpPost]
        [ProducesResponseType(typeof(PlaceOrderResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Place([FromBody] PlaceOrderRequestDto request, CancellationToken ct)
        {
            // Enforce user identity from JWT (ignore any userId sent by client)
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            request.UserId = userId.Value;

            var res = await _service.PlaceOrderAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = res.Id }, res);
        }

        /// <summary>Get order by id with items.</summary>
        [Authorize] // require login; if you want owner-or-admin only, tell me and I'll enforce it
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(OrderReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        /// <summary>Get current user's orders (paged).</summary>
        [Authorize]
        [HttpGet("mine")]
        [ProducesResponseType(typeof(PagedResult<OrderSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMine(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string? sortBy = "date",
            [FromQuery] bool desc = true,
            CancellationToken ct = default)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var result = await _service.GetUserOrdersAsync(userId.Value, page, size, sortBy, desc, ct);
            return Ok(result);
        }

        /// <summary>Cancel an order (restores inventory if eligible).</summary>
        [Authorize]
        [HttpPost("{id:int}/cancel")]
        [ProducesResponseType(typeof(CancelOrderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Cancel(
            [FromRoute] int id,
            [FromQuery] string? reason = null,
            CancellationToken ct = default)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            var result = await _service.CancelOrderAsync(id, userId.Value, isAdmin, reason, ct);
            return Ok(result);
        }

        /// <summary>Admin: list all orders with filters + paging.</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<OrderReadDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? status,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? userId,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string? sortBy = "date",
            [FromQuery] bool desc = true,
            CancellationToken ct = default)
        {
            var result = await _service.GetAllAsync(status, from, to, userId, page, size, sortBy, desc, ct);
            return Ok(result);
        }

        /// <summary>Admin: update order status (Pending, Confirmed, Shipped, Delivered, Cancelled).</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpPatch("{id:int}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateStatus(
            [FromRoute] int id,
            [FromBody] UpdateOrderStatusRequset request,   // <-- fixed name (was Requset)
            CancellationToken ct)
        {
            var ok = await _service.UpdateStatusAsync(id, request.Status, ct);
            if (!ok) return BadRequest("Order not found or invalid status.");
            return Ok(new {message="Order status updated Successfully"});
        }
    }
}