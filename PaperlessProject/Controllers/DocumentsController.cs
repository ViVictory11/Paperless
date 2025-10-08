using Microsoft.AspNetCore.Mvc;
using Paperless.Contracts;
using PaperlessProject.Exceptions;
using System.Net.Http.Json;
using System.Text.Json;

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
        try
        {
            var docs = await _http.GetFromJsonAsync<IEnumerable<DocumentDto>>("/api/documents");
            return Ok(docs);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException("Failed to reach DAL service while getting all documents.", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new ExternalServiceException("Unsupported content type from DAL response while getting documents.", ex);
        }
        catch (JsonException ex)
        {
            throw new ExternalServiceException("Error parsing JSON response from DAL while getting documents.", ex);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentDto>> Get(Guid id)
    {
        try
        {
            var doc = await _http.GetFromJsonAsync<DocumentDto>($"/api/documents/{id}");
            return doc is null ? NotFound() : Ok(doc);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException($"Failed to reach DAL service while getting document {id}.", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new ExternalServiceException($"Unsupported content type from DAL while getting document {id}.", ex);
        }
        catch (JsonException ex)
        {
            throw new ExternalServiceException($"Error parsing JSON response for document {id}.", ex);
        }
    }

    [HttpPost]
    public async Task<ActionResult<DocumentDto>> Post(CreateDocumentDto dto)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/documents", dto);
            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode);

            var created = await res.Content.ReadFromJsonAsync<DocumentDto>();
            return CreatedAtAction(nameof(Get), new { id = created!.Id }, created);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException("Failed to reach DAL service while creating document.", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new ExternalServiceException("Unsupported content type from DAL while creating document.", ex);
        }
        catch (JsonException ex)
        {
            throw new ExternalServiceException("Error parsing JSON response from DAL after creating document.", ex);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/documents/{id}");
            return StatusCode((int)res.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException($"Failed to reach DAL service while deleting document {id}.", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new ExternalServiceException($"Unsupported content type from DAL while deleting document {id}.", ex);
        }
        catch (JsonException ex)
        {
            throw new ExternalServiceException($"Error parsing JSON response from DAL after deleting document {id}.", ex);
        }
    }
}
