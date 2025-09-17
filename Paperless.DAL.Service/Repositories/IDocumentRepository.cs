using Paperless.DAL.Service.Models;
namespace Paperless.DAL.Service.Repositories;
public interface IDocumentRepository
{
    Task<DocumentEntity?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<DocumentEntity>> GetAllAsync(CancellationToken ct = default);
    Task<DocumentEntity> AddAsync(DocumentEntity entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
