using Microsoft.AspNetCore.Mvc;
using Paperless.Contracts;
using System.Text;
using PaperlessProject.Exceptions;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PaperlessProject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IHttpClientFactory factory, ILogger<DocumentsController> logger)
    {
        _http = factory.CreateClient("Dal");
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentDto>>> Get()
    {
        _logger.LogInformation("Forwarding GET request to DAL /api/documents");
        try
        {
            var docs = await _http.GetFromJsonAsync<IEnumerable<DocumentDto>>("/api/documents");
            _logger.LogInformation("Received {Count} documents from DAL.", docs?.Count() ?? 0);
            return Ok(docs);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reach DAL service while getting all documents.");
            throw new ExternalServiceException("Failed to reach DAL service while getting all documents.", ex);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported content type from DAL while getting documents.");
            throw new ExternalServiceException("Unsupported content type from DAL response while getting documents.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing JSON response from DAL while getting documents.");
            throw new ExternalServiceException("Error parsing JSON response from DAL while getting documents.", ex);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentDto>> Get(Guid id)
    {
        _logger.LogInformation("Forwarding GET /api/documents/{Id} to DAL.", id);
        try
        {
            var doc = await _http.GetFromJsonAsync<DocumentDto>($"/api/documents/{id}");
            if (doc is null)
            {
                _logger.LogWarning("DAL returned 404 for document {Id}.", id);
                return NotFound();
            }

            _logger.LogInformation("Successfully retrieved document {Id} via DAL.", id);
            return Ok(doc);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reach DAL service while getting document {Id}.", id);
            throw new ExternalServiceException($"Failed to reach DAL service while getting document {id}.", ex);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported content type from DAL while getting document {Id}.", id);
            throw new ExternalServiceException($"Unsupported content type from DAL while getting document {id}.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing JSON response for document {Id}.", id);
            throw new ExternalServiceException($"Error parsing JSON response for document {id}.", ex);
        }
    }

    [HttpPost]
    public async Task<ActionResult<DocumentDto>> Post(CreateDocumentDto dto)
    {
        _logger.LogInformation("Forwarding POST request to DAL /api/documents.");
        try
        {
            var res = await _http.PostAsJsonAsync("/api/documents", dto);
            _logger.LogInformation("DAL responded to POST with status {StatusCode}.", res.StatusCode);

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to create document. DAL returned {StatusCode}.", res.StatusCode);
                return StatusCode((int)res.StatusCode);
            }

            var created = await res.Content.ReadFromJsonAsync<DocumentDto>();
            _logger.LogInformation("Successfully created document {Id} via DAL.", created?.Id);
            return CreatedAtAction(nameof(Get), new { id = created!.Id }, created);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reach DAL service while creating document.");
            throw new ExternalServiceException("Failed to reach DAL service while creating document.", ex);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported content type from DAL while creating document.");
            throw new ExternalServiceException("Unsupported content type from DAL while creating document.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing JSON response from DAL after creating document.");
            throw new ExternalServiceException("Error parsing JSON response from DAL after creating document.", ex);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        _logger.LogInformation("Forwarding DELETE /api/documents/{Id} to DAL.", id);
        try
        {
            var res = await _http.DeleteAsync($"/api/documents/{id}");
            _logger.LogInformation("DAL responded to DELETE {Id} with {StatusCode}.", id, res.StatusCode);
            return StatusCode((int)res.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reach DAL service while deleting document {Id}.", id);
            throw new ExternalServiceException($"Failed to reach DAL service while deleting document {id}.", ex);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported content type from DAL while deleting document {Id}.", id);
            throw new ExternalServiceException($"Unsupported content type from DAL while deleting document {id}.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing JSON response from DAL after deleting document {Id}.", id);
            throw new ExternalServiceException($"Error parsing JSON response from DAL after deleting document {id}.", ex);
        }
    }
}
