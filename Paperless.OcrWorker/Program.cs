using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Paperless.OcrWorker;
using Paperless.OcrWorker.Options;
using Paperless.OcrWorker.FileStorage;
using Paperless.OcrWorker.Services;
using Microsoft.Extensions.Http;
using Elastic.Clients.Elasticsearch;
using Paperless.OcrWorker.Elasticsearch;


var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddMinioStorage(context.Configuration);
        services.AddSingleton<OCRService>();
        services.AddHttpClient<GeminiService>();
        services.AddSingleton<IElasticService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ElasticService>>();

            var settings = new ElasticsearchClientSettings(new Uri("http://elasticsearch:9200"))
                                .DefaultIndex("documents");

            var client = new ElasticsearchClient(settings);

            return new ElasticService(client, logger);
        });
        services.AddHostedService<Worker>();

    })
    .Build();

await host.RunAsync();
