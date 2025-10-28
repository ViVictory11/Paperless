using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio;
using Paperless.OcrWorker;
using Paperless.OcrWorker.Options;
using Paperless.OcrWorker.FileStorage;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<MinioOptions>(opt =>
        {
            opt.Endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT") ?? "minio:9000";
            opt.AccessKey = Environment.GetEnvironmentVariable("MINIO_ROOT_USER") ?? "minioadmin";
            opt.SecretKey = Environment.GetEnvironmentVariable("MINIO_ROOT_PASSWORD") ?? "minioadmin";
            opt.BucketName = "documents";
            opt.UseSSL = false;
        });

        services.AddSingleton<IMinioClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
            return new MinioClient()
                .WithEndpoint(options.Endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey)
                .WithSSL(options.UseSSL)
                .Build();
        });

        services.AddSingleton<IDocumentStorage, MinioDocumentStorage>();

        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
