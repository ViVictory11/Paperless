using System.IO;
using System.Threading.Tasks;

namespace Paperless.DAL.Service.Services.FileStorage
{
    public interface IDocumentStorage
    {
        Task<string> UploadAsync(Stream fileStream, string objectName, string contentType);
        Task<Stream> DownloadAsync(string objectName);
        Task DeleteAsync(string objectName);
    }
}
