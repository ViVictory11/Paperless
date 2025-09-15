using Microsoft.EntityFrameworkCore;
using Paperless.DAL.Service.Data;
using Paperless.DAL.Service.Models;

namespace Paperless.DAL.Service.Repositories;
public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _db;
    public DocumentRepository(AppDbContext db) => _db = db;

    public Task<DocumentEntity?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<List<DocumentEntity>> GetAllAsync(CancellationToken ct = default) =>
        _db.Documents.AsNoTracking().OrderBy(d => d.UploadedAt).ToListAsync(ct);

    public async Task<DocumentEntity> AddAsync(DocumentEntity entity, CancellationToken ct = default)
    {
        _db.Documents.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return false;
        _db.Documents.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
