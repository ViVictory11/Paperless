using Microsoft.EntityFrameworkCore;
using Paperless.DAL.Service.Data;
using Paperless.DAL.Service.Models;
using Paperless.DAL.Service.Exceptions;
using Microsoft.Extensions.Logging;

namespace Paperless.DAL.Service.Repositories;
public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<DocumentRepository> _logger;

    public DocumentRepository(AppDbContext db, ILogger<DocumentRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DocumentEntity?> GetAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching document with ID {Id}", id);
        try
        {
            var doc = await _db.Documents.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id, ct);

            if (doc == null)
                _logger.LogWarning("Document {Id} not found in database.", id);
            else
                _logger.LogDebug("Document {Id} successfully retrieved.", id);

            return doc;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database query failed during GetAsync for document {Id}.", id);
            throw new DatabaseConnectionException("Database query failed during GetAsync.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetAsync for document {Id}.", id);
            throw new RepositoryException("Unexpected error in GetAsync.", ex);
        }
    }
    public async Task<List<DocumentEntity>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching all documents.");
        try
        {
            var docs = await _db.Documents.AsNoTracking()
                .OrderBy(d => d.UploadedAt)
                .ToListAsync(ct);

            _logger.LogInformation("Retrieved {Count} documents from database.", docs.Count);
            return docs;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database query failed during GetAllAsync.");
            throw new DatabaseConnectionException("Database query failed during GetAllAsync.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetAllAsync.");
            throw new RepositoryException("Unexpected error in GetAllAsync.", ex);
        }
    }
    public async Task<DocumentEntity> AddAsync(DocumentEntity entity, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding new document {FileName} with ID {Id}", entity.FileName, entity.Id);
        try
        {
            _db.Documents.Add(entity);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Successfully added document {Id} to database.", entity.Id);
            return entity;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to add document {Id} to database.", entity.Id);
            throw new DatabaseConnectionException("Failed to add document to database.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in AddAsync for document {Id}.", entity.Id);
            throw new RepositoryException("Unexpected error in AddAsync.", ex);
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Attempting to delete document {Id}", id);
        try
        {
            var entity = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (entity is null)
            {
                _logger.LogWarning("Document {Id} not found. Delete aborted.", id);
                throw new DataNotFoundException($"Document with id {id} not found.");
            }

            _db.Documents.Remove(entity);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Document {Id} successfully deleted.", id);
            return true;
        }
        catch (DataNotFoundException ex)
        {
            _logger.LogWarning("Delete failed: {Message}", ex.Message);
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to delete document {Id} from database.", id);
            throw new DatabaseConnectionException("Failed to delete document from database.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in DeleteAsync for document {Id}.", id);
            throw new RepositoryException("Unexpected error in DeleteAsync.", ex);
        }
    }
}
