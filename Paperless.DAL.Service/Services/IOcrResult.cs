namespace Paperless.DAL.Service.Services
{
    public interface IOcrResult
    {
        void SaveResult(string documentId, string ocrText);
        string? GetResult(string documentId);
    }
}
