using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Products;
using ShoppingWebApi.Models.DTOs.Reviews;

namespace ShoppingWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _service;

        public ProductsController(IProductService service)
        {
            _service = service;
        }

        // ---------------------------------------
        // GET ALL PRODUCTS (public)
        // GET /api/products?page=1&size=20&sortBy=name&sortDir=asc
        // ---------------------------------------
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<ProductReadDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<ProductReadDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 20,
            [FromQuery] string? sortBy = "newest",
            [FromQuery] string? sortDir = "desc",
            CancellationToken ct = default)
        {
            var result = await _service.GetAllAsync(page, size, sortBy, sortDir, ct);
            return Ok(result);
        }

        // ---------------------------------------
        // SEARCH (public)
        // GET /api/products/search?categoryId=1&includeChildren=true...
        // ---------------------------------------
        [HttpGet("search")]
        [ProducesResponseType(typeof(PagedResult<ProductReadDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<ProductReadDto>>> Search(
            [FromQuery] ProductQuery query,
            CancellationToken ct = default)
        {
            var result = await _service.SearchAsync(query, ct);
            return Ok(result);
        }

        // ---------------------------------------
        // GET PRODUCT BY ID (public)
        // GET /api/products/10
        // ---------------------------------------
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ProductReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProductReadDto>> GetById(int id, CancellationToken ct = default)
        {
            var result = await _service.GetByIdAsync(id, ct);
            if (result == null) return NotFound();
            return Ok(result);
        }

        // ---------------------------------------
        // CREATE PRODUCT (Admin only)
        // POST /api/products
        // ---------------------------------------
        [Authorize(Policy = "AdminOnly")]
        [HttpPost]
        [ProducesResponseType(typeof(ProductReadDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ProductReadDto>> Create(
            [FromBody] ProductCreateDto dto,
            CancellationToken ct = default)
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // ---------------------------------------
        // UPDATE PRODUCT (Admin only)
        // PUT /api/products/10
        // ---------------------------------------
        [Authorize(Policy = "AdminOnly")]
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ProductReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ProductReadDto>> Update(
            int id,
            [FromBody] ProductUpdateDto dto,
            CancellationToken ct = default)
        {
            var updated = await _service.UpdateAsync(id, dto, ct);
            return Ok(new {message="Product Updated successfully"});
        }

        // ---------------------------------------
        // DELETE PRODUCT (Admin only)
        // DELETE /api/products/10
        // ---------------------------------------
        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            await _service.DeleteAsync(id, ct);
            return Ok(new {message="Producted deleted succesfully"});
        }

        // ---------------------------------------
        // ADD IMAGE TO PRODUCT (Admin only)
        // POST /api/products/10/images
        // ---------------------------------------
        [Authorize(Policy = "AdminOnly")]
        [HttpPost("{id:int}/images")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AddImage(
            int id,
            [FromBody] ProductImageCreateDto dto,
            CancellationToken ct = default)
        {
            await _service.AddImageAsync(id, dto, ct);
            return Ok(new {message="Image added Successfully"});
        }

        // ---------------------------------------
        // REMOVE IMAGE FROM PRODUCT (Admin only)
        // DELETE /api/products/10/images/2
        // ---------------------------------------
        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("{id:int}/images/{imageId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RemoveImage(
            int id,
            int imageId,
            CancellationToken ct = default)
        {
            await _service.RemoveImageAsync(id, imageId, ct);
            return Ok(new {message="Image Removed Successfully"});
        }

        // ---------------------------------------
        // ACTIVATE / DEACTIVATE PRODUCT (Admin only)
        // PATCH /api/products/10/active?isActive=false
        // ---------------------------------------
        [Authorize(Policy = "AdminOnly")]
        [HttpPatch("{id:int}/active")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SetActive(
            int id,
            [FromQuery] bool isActive,
            CancellationToken ct = default)
        {
            await _service.SetActiveAsync(id, isActive, ct);
            return Ok(new {message="Product status Changed sucessfully"});
        }

        // ---------------------------------------
        // GET REVIEWS OF A PRODUCT (public)
        // GET /api/products/10/reviews?page=1&size=10&minRating=4&sortBy=newest
        // ---------------------------------------
        [HttpGet("{id:int}/reviews")]
        [ProducesResponseType(typeof(PagedResult<ReviewReadDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<ReviewReadDto>>> GetReviewsByProductId(
            int id,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] int? minRating = null,
            [FromQuery] string? sortBy = "newest",
            [FromQuery] string? sortDir = "desc",
            CancellationToken ct = default)
        {
            var result = await _service.GetReviewsByProductIdAsync(
                id,
                page,
                size,
                minRating,
                sortBy,
                sortDir,
                ct
            );

            return Ok(result);
        }
    }
}