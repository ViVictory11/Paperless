namespace Paperless.DAL.Service.Contracts
{
    public record CreateDocumentDto(string FileName, string ContentType, long SizeBytes);
}
