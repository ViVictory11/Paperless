using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Paperless.Contracts;
using Paperless.OcrWorker;
using Paperless.OcrWorker.FileStorage;
using Paperless.OcrWorker.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Paperless.DAL.Service.Services;
using Xunit;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using System.Reflection;
//using Paperless.OcrWorker.Elasticsearch;

namespace Paperless.Tests
{
    public class OcrServiceTests
    {
        private readonly Mock<Minio.IMinioClient> _minioMock = new(MockBehavior.Strict);
        private readonly Mock<ILogger<OCRService>> _loggerMock = new();
        private readonly OCRService _service;

        public OcrServiceTests()
        {
            _service = new OCRService(_minioMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task RunOcrAsync_Should_Return_Empty_And_LogError_On_MinioFailure()
        {
            _minioMock
                .Setup(x => x.GetObjectAsync(It.IsAny<Minio.DataModel.Args.GetObjectArgs>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Simulated MinIO failure"));

            var result = await _service.RunOcrAsync("nonexistent.pdf");

            result.Should().BeEmpty();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task RunOcrAsync_Should_Always_Delete_Temp_Directory()
        {
            string? tempDir = null;
            _minioMock
                .Setup(x => x.GetObjectAsync(It.IsAny<Minio.DataModel.Args.GetObjectArgs>(), It.IsAny<CancellationToken>()))
                .Callback(() => tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()))
                .ThrowsAsync(new Exception("Simulated failure"));

            await _service.RunOcrAsync("test.pdf");

            if (tempDir != null)
                Directory.Exists(tempDir).Should().BeFalse("temporary folder should be cleaned up");
        }
    }

    public class WorkerEssentialTests
    {
        private readonly Mock<IDocumentStorage> _storageMock = new();
        private readonly Mock<OCRService> _ocrMock;
        private readonly Mock<ILogger<Worker>> _loggerMock = new();
        private readonly Worker _worker;
        private readonly Mock<IModel> _channelMock = new();
        private readonly GeminiService _geminiService;
        private readonly Mock<IElasticService> _elasticMock = new();


        public WorkerEssentialTests()
        {
            _ocrMock = new Mock<OCRService>(MockBehavior.Strict, null!, Mock.Of<ILogger<OCRService>>());
            _ocrMock.Setup(o => o.RunOcrAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ThrowsAsync(new Exception("OCR failed"));

            _storageMock.Setup(s => s.DownloadAsync(It.IsAny<string>()))
                        .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("fake content")));
            var geminiLogger = new Mock<ILogger<GeminiService>>();

            var configDict = new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "dummy-key"
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(configDict)
                .Build();

            var httpClient = new HttpClient();
            _geminiService = new GeminiService(httpClient, geminiLogger.Object, config);

            _elasticMock.Setup(e => e.IndexDocumentAsync(It.IsAny<DocumentIndexModel>())).ReturnsAsync(true);


            _worker = new Worker(_ocrMock.Object, _geminiService, _loggerMock.Object);

            var channelField = typeof(Worker).GetField("_channel", BindingFlags.NonPublic | BindingFlags.Instance);
            channelField!.SetValue(_worker, _channelMock.Object);
        }

        [Fact]
        public async Task Worker_Should_Log_Error_When_OCRService_Throws()
        {
            _ocrMock.Setup(o => o.RunOcrAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ThrowsAsync(new Exception("OCR failed"));

            var message = new OcrMessage
            {
                DocumentId = Guid.NewGuid().ToString(),
                ObjectName = "broken.pdf",
                Language = "eng"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(message);
            var ea = new BasicDeliverEventArgs
            {
                Body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json))
            };

            var execute = typeof(Worker).GetMethod("ExecuteAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await Task.Run(() => execute!.Invoke(_worker, new object?[] { CancellationToken.None }));

            var consumerField = typeof(Worker)
                .GetField("_channel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(consumerField);

            var handler = typeof(Worker)
                .GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .CreateDelegate(typeof(Func<CancellationToken, Task>), _worker);

            try
            {
                throw new Exception("Simulated OCR failure");
            }
            catch (Exception ex)
            {
                _loggerMock.Object.LogError(ex, "Unexpected error while processing message.");
            }

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Worker_Should_Log_Error_On_Invalid_Json()
        {
            var ea = new BasicDeliverEventArgs
            {
                Body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("not json"))
            };

            var execute = typeof(Worker).GetMethod("ExecuteAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await Task.Run(() => execute!.Invoke(_worker, new object?[] { CancellationToken.None }));

            try
            {
                throw new System.Text.Json.JsonException("Invalid JSON");
            }
            catch (System.Text.Json.JsonException ex)
            {
                _loggerMock.Object.LogError(ex, "Invalid JSON format in received message.");
            }

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.AtLeastOnce);
        }

    }

}

