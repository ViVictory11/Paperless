using Microsoft.EntityFrameworkCore;
using Paperless.DAL.Service.Data;
using Paperless.DAL.Service.Profiles;
using Paperless.DAL.Service.Repositories;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Paperless.DAL.Service;
using Paperless.DAL.Service.Messaging;
using Paperless.DAL.Service.Services;
using Paperless.DAL.Service.Services.FileStorage;
using Paperless.DAL.Service.Options;
using Microsoft.Extensions.Options;
using Minio;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
    config.AddDebug();
}).CreateLogger("Startup");

logger.LogInformation("Initializing Paperless.DAL.Service application...");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<DocumentProfile>();
});

builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();

builder.Services.AddSingleton<IOcrResult, OcrResult>();
builder.Services.AddHostedService<OcrResultListener>();

builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));

builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var opt = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
    var client = new MinioClient()
        .WithEndpoint(opt.Endpoint)
        .WithCredentials(opt.AccessKey, opt.SecretKey);
    if (opt.UseSSL) client = client.WithSSL();
    return client.Build();
});

builder.Services.AddSingleton<IDocumentStorage, MinioDocumentStorage>();

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
            new Minio.DataModel.Args.BucketExistsArgs().WithBucket(opt.BucketName));
        if (!exists)
        {
            await client.MakeBucketAsync(
                new Minio.DataModel.Args.MakeBucketArgs().WithBucket(opt.BucketName));
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

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

logger.LogInformation("Starting web host...");
app.Run();
