using System.IO;
using System.Threading.Tasks;
using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;
using Paperless.DAL.Service.Options;

namespace Paperless.DAL.Service.Services.FileStorage
{
    public class MinioDocumentStorage : IDocumentStorage
    {
        private readonly IMinioClient _client;
        private readonly MinioOptions _options;

        public MinioDocumentStorage(IMinioClient client, IOptions<MinioOptions> options)
        {
            _client = client;
            _options = options.Value;
        }

        public async Task<string> UploadAsync(Stream fileStream, string objectName, string contentType)
        {
            var args = new PutObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType);

            await _client.PutObjectAsync(args);
            return objectName;
        }

        public async Task<Stream> DownloadAsync(string objectName)
        {
            var ms = new MemoryStream();
            var args = new GetObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(ms));

            await _client.GetObjectAsync(args);
            ms.Position = 0;
            return ms;
        }

        public async Task DeleteAsync(string objectName)
        {
            var args = new RemoveObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName);
            await _client.RemoveObjectAsync(args);
        }
    }
}
