using System.Text;
using Minio;
using ImageMagick;
using Tesseract;
using Minio.DataModel.Notification;
using System.Security.AccessControl;

namespace Paperless.OcrWorker.Services
{
    public class OCRService
    {
        private readonly ILogger<OCRService> _logger;
        private readonly IMinioClient _minio;

        public OCRService(IMinioClient minio, ILogger<OCRService> logger)
        {
            _minio = minio;
            _logger = logger;
        }

        public virtual async Task<string> RunOcrAsync(string objectName, string lang = "deu+eng")
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            try
            {
                var pdfPath = Path.Combine(tempDir, "file.pdf");
                await DownloadMinioAsync(objectName, pdfPath);

                var settings = new MagickReadSettings
                {
                    Density = new Density(400, 400),
                    ColorSpace = ColorSpace.Gray
                };
                settings.SetDefine(MagickFormat.Pdf, "use-cropbox", "true");

                using var images = new MagickImageCollection();
                images.Read(pdfPath, settings);

                var textBuilder = new StringBuilder();
                var tessPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
                _logger.LogInformation($"Using tessdata path: {tessPath}");

                using var engine = new TesseractEngine(tessPath, lang, EngineMode.Default)
                {
                    DefaultPageSegMode = PageSegMode.Auto
                };

                foreach (var img in images)
                {
                    img.Deskew(new Percentage(40));
                    img.ContrastStretch(new Percentage(0.1), new Percentage(0.1));
                    img.AdaptiveSharpen();
                    img.Format = MagickFormat.Png;

                    using var ms = new MemoryStream();
                    img.Write(ms);
                    using var pix = Pix.LoadFromMemory(ms.ToArray());
                    using var page = engine.Process(pix);

                    var text = page.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                        textBuilder.AppendLine(text);
                }

                var result = textBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(result))
                    _logger.LogWarning("OCR completed but returned no text.");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR failed for {Id}", objectName);
                return string.Empty;
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }


        private async Task DownloadMinioAsync(string objectName, string targetPath)
        {
            await _minio.GetObjectAsync(new Minio.DataModel.Args.GetObjectArgs()
                .WithBucket("documents")
                .WithObject(objectName)
                .WithFile(targetPath));
        }
    }
}
