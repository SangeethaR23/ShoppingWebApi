using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Inventory;

namespace ShoppingWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoriesController : ControllerBase
    {
        private readonly IInventoryService _service;

        public InventoriesController(IInventoryService service)
        {
            _service = service;
        }

        /// <summary>Paged list with optional filters (product/category/SKU/low-stock).</summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<InventoryReadDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int? productId,
            [FromQuery] int? categoryId,
            [FromQuery] string? sku,
            [FromQuery] bool? lowStockOnly,
            [FromQuery] string? sortBy = "product",
            [FromQuery] bool desc = false,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            CancellationToken ct = default)
        {
            var res = await _service.GetPagedAsync(productId, categoryId, sku, lowStockOnly, sortBy, desc, page, size, ct);
            return Ok(res);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(InventoryReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        [HttpGet("by-product/{productId:int}")]
        [ProducesResponseType(typeof(InventoryReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByProduct([FromRoute] int productId, CancellationToken ct)
        {
            var dto = await _service.GetByProductIdAsync(productId, ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        /// <summary>Adjust quantity by delta (positive to add, negative to reduce).</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpPost("by-product/{productId:int}/adjust")]
        [ProducesResponseType(typeof(InventoryReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Adjust([FromRoute] int productId, [FromBody] InventoryAdjustRequestDto dto, CancellationToken ct)
        {
            var res = await _service.AdjustAsync(productId, dto.Delta, dto.Reason, ct);
            return Ok(res);
        }

        /// <summary>Set absolute quantity.</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpPut("by-product/{productId:int}/set")]
        [ProducesResponseType(typeof(InventoryReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SetQuantity([FromRoute] int productId, [FromBody] InventorySetRequestDto dto, CancellationToken ct)
        {
            var res = await _service.SetQuantityAsync(productId, dto.Quantity, ct);
            return Ok(res);
        }

        /// <summary>Set reorder level.</summary>
        [Authorize(Policy = "AdminOnly")]
        [HttpPost("by-product/{productId:int}/reorder-level")]
        [ProducesResponseType(typeof(InventoryReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SetReorderLevel([FromRoute] int productId, [FromBody] InventoryReorderLevelRequestDto dto, CancellationToken ct)
        {
            var res = await _service.SetReorderLevelAsync(productId, dto.ReorderLevel, ct);
            return Ok(res);
        }
    }
}