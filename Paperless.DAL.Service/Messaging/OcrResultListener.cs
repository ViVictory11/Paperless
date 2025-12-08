using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Paperless.Contracts;
using Paperless.DAL.Service.Repositories;
using Paperless.DAL.Service.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paperless.DAL.Service.Messaging
{
    public class OcrResultListener : BackgroundService
    {
        private readonly IOcrResult _resultStore;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IElasticService _elasticService;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly string _resultQueue = "result_queue";

        public OcrResultListener(IOcrResult resultStore, IServiceScopeFactory scopeFactory, IElasticService elasticService)
        {
            _resultStore = resultStore;
            _scopeFactory = scopeFactory;
            _elasticService = elasticService;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
                var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "user";
                var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "pass";

                var factory = new ConnectionFactory
                {
                    HostName = host,
                    UserName = user,
                    Password = pass
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.QueueDeclare(queue: _resultQueue, durable: false, exclusive: false, autoDelete: false);

                Console.WriteLine($"Listening to result queue '{_resultQueue}' on host '{host}'");
            }
            catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException ex)
            {
                Console.WriteLine($"RabbitMQ unreachable: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start OcrResultListener: {ex.Message}");
            }

            return base.StartAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null)
                throw new InvalidOperationException("Channel not initialized.");

            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var message = JsonSerializer.Deserialize<OcrMessage>(json);

                    if (message == null)
                    {
                        Console.WriteLine("Invalid or empty OCR result received.");
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

                    if (!string.IsNullOrEmpty(message.OcrText))
                    {
                        _resultStore.SaveResult(message.DocumentId, message.OcrText);
                        Console.WriteLine($"Stored OCR result for document {message.DocumentId}");
                    }

                    if (!string.IsNullOrEmpty(message.Summary))
                    {
                        Console.WriteLine($"Saving Gemini summary for document {message.DocumentId}...");
                        try
                        {
                            await repo.SaveSummaryAsync(Guid.Parse(message.DocumentId), message.Summary);
                            Console.WriteLine("Summary saved successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to save summary: {ex.Message}");
                        }
                    }

                    try
                    {
                        await _elasticService.IndexDocumentAsync(new DocumentIndexModel
                        {
                            DocumentId = message.DocumentId,
                            FileName = string.IsNullOrWhiteSpace(message.OriginalFileName)? message.ObjectName: message.OriginalFileName,
                            OriginalFileName = message.OriginalFileName,
                            Content = message.OcrText ?? "",
                            Summary = message.Summary
                        });


                        Console.WriteLine($"Indexed document {message.DocumentId} into Elasticsearch.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to index document in Elasticsearch: {ex.Message}");
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Invalid JSON received: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error in message consumer: {ex.Message}");
                }
            };

            _channel.BasicConsume(queue: _resultQueue, autoAck: true, consumer: consumer);
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _channel?.Close();
            _connection?.Close();
            return base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            try
            {
                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to dispose OcrResultListener: {ex.Message}");
            }

            base.Dispose();
        }
    }
}
