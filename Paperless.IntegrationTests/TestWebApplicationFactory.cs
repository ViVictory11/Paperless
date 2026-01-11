using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paperless.DAL.Service.Services;
using Paperless.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Paperless.DAL.Service;
using Microsoft.Extensions.Hosting;
using Paperless.DAL.Service.Messaging;


namespace Paperless.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private class FakeRabbitMqService : IRabbitMqService
    {
        public void SendMessage(string message) { }
    }

    private class FakeElasticService : IElasticService
    {
        public Task<bool> IndexDocumentAsync(DocumentIndexModel doc) =>
            Task.FromResult(true);

        public Task<IEnumerable<DocumentIndexModel>> SearchAsync(string query) =>
            Task.FromResult(Enumerable.Empty<DocumentIndexModel>());

        public Task<bool> DeleteDocumentAsync(string documentId) =>
            Task.FromResult(true);

    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=paperless;Username=postgres;Password=postgres",

                ["Minio:Endpoint"] = "localhost:9000",
                ["Minio:AccessKey"] = "minioadmin",
                ["Minio:SecretKey"] = "minioadmin",
                ["Minio:BucketName"] = "documents",

                ["RabbitMq:HostName"] = "localhost",
                ["RabbitMq:Port"] = "5672",
                ["RabbitMq:UserName"] = "user",
                ["RabbitMq:Password"] = "password",

                ["Elasticsearch:Uri"] = "http://localhost:9200"
            };

            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            var hosted = services.Where(d =>
                d.ServiceType == typeof(IHostedService) &&
                (d.ImplementationType == typeof(OcrResultListener)
                 || d.ImplementationFactory?.Method.ReturnType == typeof(OcrResultListener))
            ).ToList();

            foreach (var d in hosted)
                services.Remove(d);

            services.RemoveAll(typeof(IRabbitMqService));
            services.AddSingleton<IRabbitMqService, FakeRabbitMqService>();

            services.RemoveAll(typeof(IElasticService));
            services.AddSingleton<IElasticService, FakeElasticService>();
        });

    }
}
