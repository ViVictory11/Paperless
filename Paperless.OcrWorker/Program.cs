using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio;
using Paperless.OcrWorker;
using Paperless.OcrWorker.Options;
using Paperless.OcrWorker.FileStorage;
using Paperless.OcrWorker.Services;
using Microsoft.Extensions.Http;


var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddMinioStorage(context.Configuration);
        services.AddSingleton<OCRService>();
        services.AddHttpClient<GeminiService>();
        services.AddHostedService<Worker>();

    })
    .Build();

await host.RunAsync();
