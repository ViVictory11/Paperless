using Microsoft.EntityFrameworkCore;
using Paperless.DAL.Service.Data;
using Paperless.DAL.Service.Profiles;
using Paperless.DAL.Service.Repositories;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Paperless.DAL.Service;
using Paperless.DAL.Service.Messaging;
using Paperless.DAL.Service.Services;      

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
    }
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Database migration failed. Application startup aborted.");
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
