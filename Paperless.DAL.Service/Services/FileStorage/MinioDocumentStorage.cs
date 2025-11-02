using System.IO;
using System.Threading.Tasks;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Microsoft.Extensions.Options;
using Paperless.DAL.Service.Options;
using Paperless.DAL.Service.Exceptions;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;

namespace Paperless.DAL.Service.Services.FileStorage
{
    public class MinioDocumentStorage : IDocumentStorage
    {
        private readonly IMinioClient _client;
        private readonly MinioOptions _options;
        private readonly ILogger<MinioDocumentStorage> _logger;
        private readonly IAsyncPolicy _resiliencePolicy;

        public MinioDocumentStorage(
            IMinioClient client,
            IOptions<MinioOptions> options,
            ILogger<MinioDocumentStorage> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var retry = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    new[]
                    {
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(4)
                    },
                    (ex, delay, attempt, _) =>
                    {
                        _logger.LogWarning(ex,
                            "MinIO transient error. Retry {Attempt} after {Delay}s.",
                            attempt, delay.TotalSeconds);
                    });

            var breaker = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (ex, ts) => _logger.LogWarning(ex,
                        "MinIO circuit OPEN for {Break}s.", ts.TotalSeconds),
                    onReset: () => _logger.LogInformation("MinIO circuit RESET."),
                    onHalfOpen: () => _logger.LogInformation("MinIO circuit HALF-OPEN."));

            _resiliencePolicy = retry.WrapAsync(breaker);
        }

        public async Task<string> UploadAsync(Stream fileStream, string objectName, string contentType)
        {
            if (fileStream == null || fileStream.Length == 0)
                throw new ArgumentException("File stream cannot be empty.", nameof(fileStream));
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be empty.", nameof(objectName));

            try
            {
                await _resiliencePolicy.ExecuteAsync(async () =>
                {
                    var args = new PutObjectArgs()
                        .WithBucket(_options.BucketName)
                        .WithObject(objectName)
                        .WithStreamData(fileStream)
                        .WithObjectSize(fileStream.Length)
                        .WithContentType(contentType ?? "application/octet-stream");

                    await _client.PutObjectAsync(args);
                    _logger.LogInformation("Uploaded '{Object}' to bucket '{Bucket}'.",
                        objectName, _options.BucketName);
                });

                return objectName;
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, "MinIO error during upload of {Object}.", objectName);
                throw new StorageException("failed to upload file to MinIO storage.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "unexpected error during upload of {Object}.", objectName);
                throw new StorageException("Unexpected error during upload.", ex);
            }
        }

        public async Task<Stream> DownloadAsync(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be empty.", nameof(objectName));

            var mem = new MemoryStream();

            try
            {
                await _resiliencePolicy.ExecuteAsync(async () =>
                {
                    var args = new GetObjectArgs()
                        .WithBucket(_options.BucketName)
                        .WithObject(objectName)
                        .WithCallbackStream(stream => stream.CopyTo(mem));

                    await _client.GetObjectAsync(args);
                });

                mem.Position = 0;
                _logger.LogInformation("Downloaded '{Object}' from bucket '{Bucket}'.",
                    objectName, _options.BucketName);
                return mem;
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, "MinIO error while downloading {Object}.", objectName);
                throw new StorageException("Failed to download file from MinIO storage.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during download of {Object}.", objectName);
                throw new StorageException("Unexpected error during download.", ex);
            }
        }

        public async Task DeleteAsync(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be empty.", nameof(objectName));

            try
            {
                await _resiliencePolicy.ExecuteAsync(async () =>
                {
                    var args = new RemoveObjectArgs()
                        .WithBucket(_options.BucketName)
                        .WithObject(objectName);

                    await _client.RemoveObjectAsync(args);
                    _logger.LogInformation("Deleted '{Object}' from bucket '{Bucket}'.",
                        objectName, _options.BucketName);
                });
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, "MinIO error while deleting {Object}.", objectName);
                throw new StorageException("Failed to delete file from MinIO storage.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during delete of {Object}.", objectName);
                throw new StorageException("Unexpected error during delete.", ex);
            }
        }
    }
}
