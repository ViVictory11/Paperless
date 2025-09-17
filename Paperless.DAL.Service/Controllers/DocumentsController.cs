using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Paperless.Contracts;
using Paperless.DAL.Service.Models;
using Paperless.DAL.Service.Repositories;

namespace Paperless.DAL.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentRepository _repo;
    private readonly IMapper _mapper;

    public DocumentsController(IDocumentRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DocumentDto>))]
    public async Task<ActionResult<IEnumerable<DocumentDto>>> GetAll(CancellationToken ct)
    {
        var entities = await _repo.GetAllAsync(ct);
        return Ok(_mapper.Map<IEnumerable<DocumentDto>>(entities));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await _repo.GetAsync(id, ct);
        if (entity is null) return NotFound();
        return Ok(_mapper.Map<DocumentDto>(entity));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(DocumentDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentDto>> Create([FromBody] CreateDocumentDto dto, CancellationToken ct)
    {
        var entity = _mapper.Map<DocumentEntity>(dto);
        // entity.Id = Guid.NewGuid();
        // entity.UploadedAt = DateTime.UtcNow;

        entity = await _repo.AddAsync(entity, ct);
        var result = _mapper.Map<DocumentDto>(entity);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var removed = await _repo.DeleteAsync(id, ct);
        return removed ? NoContent() : NotFound();
    }
}
