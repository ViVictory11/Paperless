using System.Collections.Concurrent;

namespace Paperless.DAL.Service.Services
{
    public class OcrResult : IOcrResult
    {
        private readonly ConcurrentDictionary<string, string> _store = new();

        public void SaveResult(string documentId, string ocrText)
        {
            _store[documentId] = ocrText;
        }

        public string? GetResult(string documentId)
        {
            _store.TryGetValue(documentId, out var result);
            return result;
        }
    }
}
