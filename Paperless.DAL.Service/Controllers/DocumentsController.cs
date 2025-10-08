using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Paperless.Contracts;
using Paperless.DAL.Service;
using Paperless.DAL.Service.Models;
using Paperless.DAL.Service.Repositories;
using Paperless.DAL.Service.Services;

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
            var docs = await _repo.GetAllAsync(ct);
            return Ok(_mapper.Map<IEnumerable<DocumentDto>>(docs));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken ct)
        {
            var doc = await _repo.GetAsync(id, ct);
            if (doc == null) return NotFound();
            return Ok(_mapper.Map<DocumentDto>(doc));
        }

        [HttpPost("upload")]
        [RequestSizeLimit(100_000_000)] 
        public async Task<ActionResult<IEnumerable<DocumentDto>>> Upload(CancellationToken ct)
        {
            var files = Request.Form?.Files;
            if (files is null || files.Count == 0)
                return BadRequest("No files in form-data.");

            var uploadsRel = Path.Combine("Assets", "Uploads");
            var uploadsAbs = Path.Combine(Directory.GetCurrentDirectory(), uploadsRel);
            Directory.CreateDirectory(uploadsAbs);

            var created = new List<DocumentDto>();

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var newId = Guid.NewGuid();
                var ext = Path.GetExtension(file.FileName);
                var storedName = $"{newId}{ext}";
                var absPath = Path.Combine(uploadsAbs, storedName);

                using (var fs = new FileStream(absPath, FileMode.Create))
                    await file.CopyToAsync(fs, ct);

                var entity = new DocumentEntity
                {
                    Id = newId,
                    FileName = file.FileName,
                    ContentType = file.ContentType ?? "application/octet-stream",
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var success = await _repo.DeleteAsync(id, ct);
            if (!success) return NotFound();
            return NoContent();
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
