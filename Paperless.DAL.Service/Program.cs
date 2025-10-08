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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate(); 
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
