using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Paperless.OcrWorker.FileStorage
{
    public interface IDocumentStorage
    {
        Task<string> UploadAsync(Stream fileStream, string objectName, string contentType);
        Task<Stream> DownloadAsync(string objectName);
        Task DeleteAsync(string objectName);
    }
}
