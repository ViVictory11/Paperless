using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paperless.Contracts;

namespace Paperless.DAL.Service.Services
{
    public class OcrResult : IOcrResult
    {
        private readonly ConcurrentDictionary<string, string> _store = new();
        private readonly ILogger<OcrResult> _logger;

        public OcrResult(ILogger<OcrResult> logger)
        {
            _logger = logger;
        }

        public void SaveResult(string documentId, string ocrText)
        {
            _store[documentId] = ocrText;
            _logger.LogInformation("OCR result for {Id} saved in memory.", documentId);
        }

        public string? GetResult(string documentId)
        {
            _store.TryGetValue(documentId, out var result);
            return result;
        }
    }
}
