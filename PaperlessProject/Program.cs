var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
    config.AddDebug();
}).CreateLogger("Startup");

logger.LogInformation("Starting PaperlessProject (API Gateway)...");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("Dal", client =>
{
    client.BaseAddress = new Uri("http://localhost:8080");
});


var app = builder.Build();
logger.LogInformation("Environment: {Env}", app.Environment.EnvironmentName);

if (app.Environment.IsDevelopment())
{
    logger.LogInformation("Running in Development mode. Enabling Swagger and Developer Exception Page.");
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

logger.LogInformation("PaperlessProject started and listening for requests...");
app.Run();
