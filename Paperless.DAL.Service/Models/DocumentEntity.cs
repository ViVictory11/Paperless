namespace Paperless.DAL.Service.Models
{
    public class DocumentEntity
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = default!;
        public string ContentType { get; set; } = default!;
        public long SizeBytes { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
