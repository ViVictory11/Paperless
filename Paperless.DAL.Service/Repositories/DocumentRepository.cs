using Microsoft.EntityFrameworkCore;
using Paperless.DAL.Service.Data;
using Paperless.DAL.Service.Models;
using Paperless.DAL.Service.Exceptions;

namespace Paperless.DAL.Service.Repositories;
public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _db;
    public DocumentRepository(AppDbContext db) => _db = db;

    public async Task<DocumentEntity?> GetAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            return await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        }
        catch (DbUpdateException ex)
        {
            throw new DatabaseConnectionException("Database query failed during GetAsync.", ex);
        }
        catch (Exception ex)
        {
            throw new RepositoryException("Unexpected error in GetAsync.", ex);
        }
    }
    public async Task<List<DocumentEntity>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Documents.AsNoTracking().OrderBy(d => d.UploadedAt).ToListAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw new DatabaseConnectionException("Database query failed during GetAllAsync.", ex);
        }
        catch (Exception ex)
        {
            throw new RepositoryException("Unexpected error in GetAllAsync.", ex);
        }
    }
    public async Task<DocumentEntity> AddAsync(DocumentEntity entity, CancellationToken ct = default)
    {
        try
        {
            _db.Documents.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }
        catch (DbUpdateException ex)
        {
            throw new DatabaseConnectionException("Failed to add document to database.", ex);
        }
        catch (Exception ex)
        {
            throw new RepositoryException("Unexpected error in AddAsync.", ex);
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var entity = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (entity is null)
                throw new DataNotFoundException($"Document with id {id} not found.");

            _db.Documents.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DataNotFoundException) { throw; }
        catch (DbUpdateException ex)
        {
            throw new DatabaseConnectionException("Failed to delete document from database.", ex);
        }
        catch (Exception ex)
        {
            throw new RepositoryException("Unexpected error in DeleteAsync.", ex);
        }
    }
}
