namespace Paperless.DAL.Service.Options
{
    public class MinioOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public bool UseSSL { get; set; } = false;
        public int PresignExpirySeconds { get; set; } = 3600;

        public MinioOptions() { }
    }
}
