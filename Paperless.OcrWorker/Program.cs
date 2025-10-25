using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Paperless.OcrWorker;
using Paperless.OcrWorker.Options;

var builder = Host.CreateApplicationBuilder(args);


builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
    config.AddDebug();
}).CreateLogger("OcrWorker");

logger.LogInformation("Initializing Paperless.OcrWorker...");

builder.Services.AddHostedService<Worker>();


builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));

builder.Services.AddSingleton(sp =>
{
    var opt = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
    var client = new MinioClient()
        .WithEndpoint(opt.Endpoint)
        .WithCredentials(opt.AccessKey, opt.SecretKey);
    if (opt.UseSSL)
        client = client.WithSSL();
    return client.Build();
});

logger.LogInformation("MinIO client configured for endpoint {Endpoint}.",
    builder.Configuration["Minio:Endpoint"] ?? "(none)");

var host = builder.Build();

logger.LogInformation("Paperless.OcrWorker started.");
await host.RunAsync();
