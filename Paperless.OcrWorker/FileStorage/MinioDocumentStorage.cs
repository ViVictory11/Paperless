using Microsoft.Extensions.Options;
using Minio.DataModel.Args;
using Minio;
using Paperless.OcrWorker.FileStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Paperless.OcrWorker.Options;
using Minio.Exceptions;


namespace Paperless.OcrWorker.FileStorage
{
    public class MinioDocumentStorage : IDocumentStorage
    {
        private readonly IMinioClient _client;
        private readonly MinioOptions _options;
        private readonly ILogger<MinioDocumentStorage> _logger;

        public MinioDocumentStorage(IMinioClient client, IOptions<MinioOptions> options, ILogger<MinioDocumentStorage> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> UploadAsync(Stream fileStream, string objectName, string contentType)
        {
            if (fileStream == null || fileStream.Length == 0)
                throw new ArgumentException("File stream cannot be empty.", nameof(fileStream));
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be empty.", nameof(objectName));

            try
            {
                var args = new PutObjectArgs()
                    .WithBucket(_options.BucketName)
                    .WithObject(objectName)
                    .WithStreamData(fileStream)
                    .WithObjectSize(fileStream.Length)
                    .WithContentType(contentType ?? "application/octet-stream");

                await _client.PutObjectAsync(args);
                _logger.LogInformation("Uploaded '{Object}' to bucket '{Bucket}'.", objectName, _options.BucketName);

                return objectName;
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, "MinIO upload failed for object '{Object}'.", objectName);
                throw new Exception($"Failed to upload '{objectName}' to MinIO bucket '{_options.BucketName}'.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while uploading '{Object}'.", objectName);
                throw;
            }
        }

        public async Task<Stream> DownloadAsync(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be empty.", nameof(objectName));

            var mem = new MemoryStream();

            try
            {
                var args = new GetObjectArgs()
                    .WithBucket(_options.BucketName)
                    .WithObject(objectName)
                    .WithCallbackStream(stream => stream.CopyTo(mem));

                await _client.GetObjectAsync(args);
                mem.Position = 0;

                _logger.LogInformation("Downloaded '{Object}' from bucket '{Bucket}'.",
                    objectName, _options.BucketName);

                return mem;
            }
            catch (ObjectNotFoundException)
            {
                _logger.LogWarning("MinIO object '{Object}' not found in bucket '{Bucket}'.", objectName, _options.BucketName);
                throw new FileNotFoundException($"File '{objectName}' not found in bucket '{_options.BucketName}'.");
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, "MinIO download failed for object '{Object}'.", objectName);
                throw new Exception($"Failed to download '{objectName}' from MinIO bucket '{_options.BucketName}'.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while downloading '{Object}'.", objectName);
                throw;
            }
        }

        public async Task DeleteAsync(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be empty.", nameof(objectName));

            try
            {
                var args = new RemoveObjectArgs()
                    .WithBucket(_options.BucketName)
                    .WithObject(objectName);

                await _client.RemoveObjectAsync(args);
                _logger.LogInformation("Deleted '{Object}' from bucket '{Bucket}'.", objectName, _options.BucketName);
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, "MinIO delete failed for object '{Object}'.", objectName);
                throw new Exception($"Failed to delete '{objectName}' from MinIO bucket '{_options.BucketName}'.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while deleting '{Object}'.", objectName);
                throw;
            }
        }
    }
}


