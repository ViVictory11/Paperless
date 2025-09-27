namespace Paperless.Contracts
{
    public record CreateDocumentDto(string FileName, string ContentType, long SizeBytes);
}

