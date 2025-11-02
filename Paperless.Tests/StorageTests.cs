using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Response;
using Moq;
using Paperless.DAL.Service.Options;
using Paperless.DAL.Service.Services.FileStorage;
using Xunit;

namespace Paperless.Tests
{
    public class StorageTests
    {
        private readonly Mock<IMinioClient> _minioMock;
        private readonly Mock<ILogger<MinioDocumentStorage>> _loggerMock;
        private readonly MinioDocumentStorage _storage;

        public StorageTests()
        {
            _minioMock = new Mock<IMinioClient>(MockBehavior.Strict);
            _loggerMock = new Mock<ILogger<MinioDocumentStorage>>();

            var opts = Options.Create(new MinioOptions
            {
                BucketName = "documents",
                Endpoint = "localhost:9000",
                AccessKey = "test",
                SecretKey = "test"
            });

            _storage = new MinioDocumentStorage(_minioMock.Object, opts, _loggerMock.Object);
        }

        [Fact]
        public async Task UploadAsync_Should_Call_PutObjectAsync()
        {
            // Arrange
            var response = new PutObjectResponse(
                HttpStatusCode.OK,
                "etag123",
                new Dictionary<string, string>(),
                3,
                "file.pdf"
            );

            _minioMock
                .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectArgs>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            using var ms = new MemoryStream(new byte[] { 1, 2, 3 });

            // Act
            await _storage.UploadAsync(ms, "file.pdf", "application/pdf");

            // Assert
            _minioMock.Verify(
                x => x.PutObjectAsync(It.IsAny<PutObjectArgs>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DownloadAsync_Should_Call_GetObjectAsync()
        {
            // Arrange
            // Create a dummy ObjectStat instance even though constructor is internal
            var fakeObjectStat = (ObjectStat)FormatterServices.GetUninitializedObject(typeof(ObjectStat));

            _minioMock
                .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectArgs>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeObjectStat);

            // Act
            await _storage.DownloadAsync("file.pdf");

            // Assert
            _minioMock.Verify(
                x => x.GetObjectAsync(It.IsAny<GetObjectArgs>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_Should_Call_RemoveObjectAsync()
        {
            // Arrange
            _minioMock
                .Setup(x => x.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _storage.DeleteAsync("file.pdf");

            // Assert
            _minioMock.Verify(
                x => x.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
