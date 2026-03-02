using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Models.DTOs.Categories;
using ShoppingWebApi.Models.DTOs.Common;

namespace ShoppingWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _service;

        public CategoriesController(ICategoryService service)
        {
            _service = service;
        }

        // GET: api/categories?page=1&size=20&sortBy=name&sortDir=asc
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<CategoryReadDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<CategoryReadDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 20,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortDir = "asc",
            CancellationToken ct = default)
        {
            var result = await _service.GetAllAsync(page, size, sortBy, sortDir, ct);
            return Ok(result);
        }

        // GET: api/categories/5
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(CategoryReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CategoryReadDto>> GetById(int id, CancellationToken ct = default)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            if (dto == null) return NotFound();
            return Ok(dto);
        }

        // POST: api/categories  (Admin only)
        [Authorize(Policy = "AdminOnly")]
        [HttpPost]
        [ProducesResponseType(typeof(CategoryReadDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CategoryReadDto>> Create([FromBody] CategoryCreateDto dto, CancellationToken ct = default)
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // PUT: api/categories/5  (Admin only)
        [Authorize(Policy = "AdminOnly")]
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(CategoryReadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CategoryReadDto>> Update(int id, [FromBody] CategoryUpdateDto dto, CancellationToken ct = default)
        {
            var updated = await _service.UpdateAsync(id, dto, ct);
            return Ok(updated);
        }

        // DELETE: api/categories/5  (Admin only)
        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            await _service.DeleteAsync(id, ct);
            return Ok(new { message ="Categories deleted successfully"});
        }
    }
}