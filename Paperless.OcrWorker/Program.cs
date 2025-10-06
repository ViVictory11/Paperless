using Paperless.OcrWorker;
using Microsoft.Extensions.Hosting;


var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
