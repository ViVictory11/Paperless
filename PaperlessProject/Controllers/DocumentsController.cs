using Microsoft.AspNetCore.Mvc;
using Paperless.Contracts;

namespace PaperlessProject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly HttpClient _http;

    public DocumentsController(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("Dal");
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentDto>>> Get()
    {
        var docs = await _http.GetFromJsonAsync<IEnumerable<DocumentDto>>("/api/documents");
        return Ok(docs);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentDto>> Get(Guid id)
    {
        var doc = await _http.GetFromJsonAsync<DocumentDto>($"/api/documents/{id}");
        return doc is null ? NotFound() : Ok(doc);
    }

    [HttpPost]
    public async Task<ActionResult<DocumentDto>> Post(CreateDocumentDto dto)
    {
        var res = await _http.PostAsJsonAsync("/api/documents", dto);
        if (!res.IsSuccessStatusCode) return StatusCode((int)res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<DocumentDto>();
        return CreatedAtAction(nameof(Get), new { id = created!.Id }, created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var res = await _http.DeleteAsync($"/api/documents/{id}");
        return StatusCode((int)res.StatusCode);
    }
}
