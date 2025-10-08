using System.Security.Cryptography.X509Certificates;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Paperless.Contracts;
using Paperless.DAL.Service;
using Paperless.DAL.Service.Models;
using Paperless.DAL.Service.Repositories;
using Paperless.DAL.Service.Services;
using Paperless.DAL.Service.Exceptions;

namespace Paperless.DAL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _repo;
        private readonly IMapper _mapper;
        private readonly IRabbitMqService _rabbitMqService;

        public DocumentsController(IDocumentRepository repo, IMapper mapper, IRabbitMqService rabbitMqService)
        {
            _repo = repo;
            _mapper = mapper;
            _rabbitMqService = rabbitMqService;
        }


        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentDto>>> GetAll(CancellationToken ct)
        {
            try
            {
                var docs = await _repo.GetAllAsync(ct);
                return Ok(_mapper.Map<IEnumerable<DocumentDto>>(docs));
            }
            catch (RepositoryException ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var doc = await _repo.GetAsync(id, ct);
                if (doc == null)
                    return NotFound();

                return Ok(_mapper.Map<DocumentDto>(doc));
            }
            catch (RepositoryException ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("upload")]
        [RequestSizeLimit(100_000_000)] 
        public async Task<ActionResult<IEnumerable<DocumentDto>>> Upload(CancellationToken ct)
        {
            try
            {
                var files = Request.Form?.Files;
                if (files is null || files.Count == 0)
                    return BadRequest("No files in form-data.");

                var uploadsRel = Path.Combine("Assets", "Uploads");
                var uploadsAbs = Path.Combine(Directory.GetCurrentDirectory(), uploadsRel);
                Directory.CreateDirectory(uploadsAbs);

                var created = new List<DocumentDto>();

                static bool HasPdfExtension(string fileName) =>
                    string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase);

                static async Task<bool> LooksLikePdfAsync(IFormFile file, CancellationToken ct)
                {
                    try
                    {
                        using var stream = file.OpenReadStream();
                        if (!stream.CanRead) return false;
                        var head = new byte[5];
                        var read = await stream.ReadAsync(head, 0, head.Length, ct);
                        if (read < 5) return false;
                        var sig = System.Text.Encoding.ASCII.GetString(head);
                        return sig == "%PDF-";
                    }
                    catch { return false; }
                }

                foreach (var file in files)
                {
                    if (file.Length == 0) continue;

                    if (file.Length > 10 * 1024 * 1024)
                        return BadRequest($"{file.FileName}: File too large (>10 MB).");

                    var hasPdfExt = HasPdfExtension(file.FileName);
                    var looksPdf = await LooksLikePdfAsync(file, ct);

                    if (!(hasPdfExt && looksPdf))
                        return BadRequest($"{file.FileName}: Only PDF files are allowed.");

                    var newId = Guid.NewGuid();
                    var storedName = $"{newId}.pdf";
                    var absPath = Path.Combine(uploadsAbs, storedName);

                    using (var fs = new FileStream(absPath, FileMode.Create))
                        await file.CopyToAsync(fs, ct);

                    var entity = new DocumentEntity
                    {
                        Id = newId,
                        FileName = file.FileName,
                        ContentType = "application/pdf",
                        SizeBytes = file.Length,
                        UploadedAt = DateTime.UtcNow
                    };

                    entity = await _repo.AddAsync(entity, ct);
                    created.Add(_mapper.Map<DocumentDto>(entity));
                    var ocrMsg = new OcrMessage
                    {
                        DocumentId = entity.Id.ToString(),
                        FilePath = absPath
                    };
                    _rabbitMqService.SendMessage(System.Text.Json.JsonSerializer.Serialize(ocrMsg));
                }

                return CreatedAtAction(nameof(GetAll), null, created);
            }
            catch (RepositoryException ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var success = await _repo.DeleteAsync(id, ct);
                if (!success) return NotFound();
                return NoContent();
            }
            catch (RepositoryException ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        //Das ist damit abgefragt wird, ob ein result da ist
        [HttpGet("/api/ocr/result/{id}")]
        public IActionResult GetOcrResult(Guid id, [FromServices] IOcrResult ocrStore)
        {
            var result = ocrStore.GetResult(id.ToString());
            if (result == null)
                return StatusCode(202);
            return Ok(new { ocrText = result });
        }

    }
}
