using Microsoft.AspNetCore.Mvc;
using Paperless.DAL.Service.Services.FileStorage;
using System.Text;

namespace Paperless.DAL.Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly IDocumentStorage _storage;

        public DebugController(IDocumentStorage storage)
        {
            _storage = storage;
        }

        [HttpPost("upload-test")]
        public async Task<IActionResult> UploadTest()
        {
            var content = "Hello from Paperless + MinIO!";
            var bytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes);

            var objectName = $"test-{Guid.NewGuid()}.txt";
            await _storage.UploadAsync(stream, objectName, "text/plain");

            return Ok(new { message = "Uploaded successfully", objectName });
        }

        [HttpGet("download-test/{objectName}")]
        public async Task<IActionResult> DownloadTest(string objectName)
        {
            var stream = await _storage.DownloadAsync(objectName);
            return File(stream, "text/plain", objectName);
        }
    }
}
