using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Paperless.Contracts;
using Paperless.DAL.Service;
using Paperless.DAL.Service.Models;
using Paperless.DAL.Service.Repositories;
using Paperless.DAL.Service.Services;
using Paperless.DAL.Service.Exceptions;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Paperless.DAL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _repo;
        private readonly IMapper _mapper;
        private readonly IRabbitMqService _rabbitMqService;
        private readonly ILogger<DocumentsController> _logger;


        public DocumentsController(IDocumentRepository repo, IMapper mapper, IRabbitMqService rabbitMqService, ILogger<DocumentsController> logger)
        {
            _repo = repo;
            _mapper = mapper;
            _rabbitMqService = rabbitMqService;
            _logger = logger;
        }


        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentDto>>> GetAll(CancellationToken ct)
        {
            _logger.LogInformation("GET /api/documents called.");
            try
            {
                var docs = await _repo.GetAllAsync(ct);
                _logger.LogInformation("Fetched {Count} documents from database.", docs.Count);
                return Ok(_mapper.Map<IEnumerable<DocumentDto>>(docs));
            }
            catch (RepositoryException ex)
            {
                _logger.LogError(ex, "RepositoryException occurred in GetAll.");
                return StatusCode(500, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetAll.");
                return StatusCode(500, new { message = $"Unexpected error: {ex.Message}" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken ct)
        {
            _logger.LogInformation("GET /api/documents/{Id} called.", id);
            try
            {
                var doc = await _repo.GetAsync(id, ct);
                if (doc == null)
                {
                    _logger.LogWarning("Document with ID {Id} not found.", id);
                    return NotFound();
                }

                _logger.LogInformation("Fetched document {Id} successfully.", id);
                return Ok(_mapper.Map<DocumentDto>(doc));
            }
            catch (RepositoryException ex)
            {
                _logger.LogError(ex, "RepositoryException occurred while fetching document {Id}.", id);
                return StatusCode(500, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetById for document {Id}.", id);
                return StatusCode(500, new { message = $"Unexpected error: {ex.Message}" });
            }
        }

        [HttpPost("upload")]
        [RequestSizeLimit(100_000_000)] 
        public async Task<ActionResult<IEnumerable<DocumentDto>>> Upload(CancellationToken ct)
        {
            _logger.LogInformation("POST /api/documents/upload called.");
            try
            {
                var files = Request.Form?.Files;
                if (files is null || files.Count == 0)
                {
                    _logger.LogWarning("Upload failed: no files provided in form-data.");
                    return BadRequest("No files in form-data.");
                }

                var uploadsRel = Path.Combine("Assets", "Uploads");
                var uploadsAbs = Path.Combine(Directory.GetCurrentDirectory(), uploadsRel);
                Directory.CreateDirectory(uploadsAbs);

                _logger.LogInformation("Saving uploaded files to {Path}", uploadsAbs);

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
                    _logger.LogDebug("Processing uploaded file {FileName} ({Size} bytes)", file.FileName, file.Length);

                    if (file.Length == 0)
                    {
                        _logger.LogWarning("File {FileName} skipped (size = 0).", file.FileName);
                        continue;
                    }

                    if (file.Length > 10 * 1024 * 1024)
                    {
                        _logger.LogWarning("File {FileName} too large (>10MB).", file.FileName);
                        return BadRequest($"{file.FileName}: File too large (>10 MB).");
                    }

                    var hasPdfExt = HasPdfExtension(file.FileName);
                    var looksPdf = await LooksLikePdfAsync(file, ct);

                    if (!(hasPdfExt && looksPdf))
                    {
                        _logger.LogWarning("File {FileName} rejected (not valid PDF).", file.FileName);
                        return BadRequest($"{file.FileName}: Only PDF files are allowed.");
                    }

                    var newId = Guid.NewGuid();
                    var storedName = $"{newId}.pdf";
                    var absPath = Path.Combine(uploadsAbs, storedName);

                    using (var fs = new FileStream(absPath, FileMode.Create))
                        await file.CopyToAsync(fs, ct);

                    _logger.LogInformation("File {FileName} saved as {StoredName}.", file.FileName, storedName);

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

                    _rabbitMqService.SendMessage(JsonSerializer.Serialize(ocrMsg));
                    _logger.LogInformation("OCR message sent for document {Id}.", entity.Id);
                }

                _logger.LogInformation("Upload completed: {Count} file(s) processed successfully.", created.Count);
                return CreatedAtAction(nameof(GetAll), null, created);
            }
            catch (RepositoryException ex)
            {
                _logger.LogError(ex, "RepositoryException during file upload.");
                return StatusCode(500, new { message = ex.Message });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error during file upload.");
                return StatusCode(500, new { message = $"File I/O error: {ex.Message}" });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied during file upload.");
                return StatusCode(500, new { message = $"Access denied: {ex.Message}" });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization error during upload.");
                return StatusCode(500, new { message = $"JSON serialization error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file upload.");
                return StatusCode(500, new { message = $"Unexpected error: {ex.Message}" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            _logger.LogInformation("DELETE /api/documents/{Id} called.", id);
            try
            {
                var success = await _repo.DeleteAsync(id, ct);
                if (!success)
                {
                    _logger.LogWarning("Delete failed: document {Id} not found.", id);
                    return NotFound();
                }

                _logger.LogInformation("Document {Id} deleted successfully.", id);
                return NoContent();
            }
            catch (RepositoryException ex)
            {
                _logger.LogError(ex, "RepositoryException during Delete for document {Id}.", id);
                return StatusCode(500, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Delete for document {Id}.", id);
                return StatusCode(500, new { message = $"Unexpected error: {ex.Message}" });
            }
        }

        //Das ist damit abgefragt wird, ob ein result da ist
        [HttpGet("/api/ocr/result/{id}")]
        public IActionResult GetOcrResult(Guid id, [FromServices] IOcrResult ocrStore)
        {
            _logger.LogInformation("GET /api/ocr/result/{Id} called.", id);
            try
            {
                var result = ocrStore.GetResult(id.ToString());
                if (result == null)
                {
                    _logger.LogDebug("No OCR result yet for document {Id}. Returning 202.", id);
                    return StatusCode(202);
                }

                _logger.LogInformation("Returning OCR result for document {Id}.", id);
                return Ok(new { ocrText = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetOcrResult for document {Id}.", id);
                return StatusCode(500, new { message = $"Unexpected error: {ex.Message}" });
            }
        }

    }
}
