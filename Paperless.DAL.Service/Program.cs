using Microsoft.EntityFrameworkCore;
using Paperless.DAL.Service.Data;
using Paperless.DAL.Service.Profiles;
using Paperless.DAL.Service.Repositories;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Paperless.DAL.Service;
using Paperless.DAL.Service.Messaging;
using Paperless.DAL.Service.Services;
using Paperless.DAL.Service.Services.FileStorage;
using Paperless.DAL.Service.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Elastic.Clients.Elasticsearch;
using Paperless.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
    config.AddDebug();
}).CreateLogger("Startup");

logger.LogInformation("Initializing Paperless.DAL.Service application...");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<DocumentProfile>());

builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddSingleton<IOcrResult, OcrResult>();
builder.Services.AddHostedService<OcrResultListener>();

builder.Services.AddMinioStorage(builder.Configuration);

builder.Services.AddSingleton<IElasticService>(sp =>
{
    var elasticLogger = sp.GetRequiredService<ILogger<ElasticService>>();

    var settings = new ElasticsearchClientSettings(
        new Uri("http://elasticsearch:9200")  
    ).DefaultIndex("documents");

    var client = new ElasticsearchClient(settings);
    return new ElasticService(client, elasticLogger);
});

var app = builder.Build();

logger.LogInformation("Paperless.DAL.Service running in {Environment} environment.", app.Environment.EnvironmentName);

try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        logger.LogInformation("Running database migrations...");
        db.Database.Migrate();
        logger.LogInformation("Database migrations completed successfully.");

        var client = scope.ServiceProvider.GetRequiredService<IMinioClient>();
        var opt = scope.ServiceProvider.GetRequiredService<IOptions<MinioOptions>>().Value;

        logger.LogInformation("Checking if MinIO bucket '{Bucket}' exists...", opt.BucketName);
        bool exists = await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(opt.BucketName));

        if (!exists)
        {
            await client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(opt.BucketName));
            logger.LogInformation("Created new MinIO bucket '{Bucket}'.", opt.BucketName);
        }
        else
        {
            logger.LogInformation("MinIO bucket '{Bucket}' already exists.", opt.BucketName);
        }
    }
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Startup initialization failed (DB or MinIO).");
    throw;
}

if (app.Environment.IsDevelopment())
{
    logger.LogInformation("Enabling Swagger UI for Development environment.");
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

logger.LogInformation("Starting web host...");
app.Run();
