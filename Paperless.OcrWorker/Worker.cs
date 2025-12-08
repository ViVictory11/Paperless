using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paperless.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Paperless.OcrWorker.FileStorage;
using Paperless.OcrWorker.Services;

namespace Paperless.OcrWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly OCRService _ocrService;
        private readonly GeminiService _geminiService;


        private IConnection? _connection;
        private IModel? _channel;
        private string _requestQueue = "document_queue";
        private string _resultQueue = "result_queue";

        public Worker(OCRService ocrService, GeminiService geminiService, ILogger<Worker> logger)
        {
            _ocrService = ocrService;
            _geminiService = geminiService;
            _logger = logger;
        }
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
                var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "user";
                var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "pass";
                _requestQueue = Environment.GetEnvironmentVariable("RABBITMQ_QUEUE") ?? "document_queue";

                var factory = new ConnectionFactory
                {
                    HostName = host,
                    UserName = user,
                    Password = pass
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.QueueDeclare(queue: _requestQueue, durable: false, exclusive: false, autoDelete: false);
                _channel.QueueDeclare(queue: _resultQueue, durable: false, exclusive: false, autoDelete: false);

                _logger.LogInformation("OCR Worker connected to RabbitMQ at {Host}", host);
            }
            catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException ex)
            {
                _logger.LogError(ex, "RabbitMQ broker unreachable.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ connection.");
            }

            return base.StartAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null)
                throw new InvalidOperationException("Channel not initialized.");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var message = JsonSerializer.Deserialize<OcrMessage>(json);

                    if (message == null)
                    {
                        _logger.LogWarning("Invalid OCR message received.");
                        return;
                    }

                    _logger.LogWarning("WORKER DEBUG => Received: DocumentId={ID}, OriginalFileName='{OFN}', ObjectName='{OBJ}'",message.DocumentId,message.OriginalFileName,message.ObjectName);


                    _logger.LogInformation("Processing OCR job for DocumentId: {Id}", message.DocumentId);

                    var ocrText = await _ocrService.RunOcrAsync(message.ObjectName);
                    message.OcrText = ocrText;
                    _logger.LogInformation($"OCR extracted {ocrText.Length} chars for {message.ObjectName}");

                    if (message.IsSummaryAllowed)
                    {
                        _logger.LogInformation("Starting Gemini summarization...");
                        var summary = await _geminiService.SummarizeAsync(ocrText);
                        message.Summary = summary;
                        _logger.LogInformation("Gemini summary created with length {Length}", summary?.Length ?? 0);
                    }
                    else
                    {
                        _logger.LogInformation("Skipping Gemini summarization (summary already exists in DB)");
                        message.Summary = null;
                    }


                    var resultJson = JsonSerializer.Serialize(message);
                    var body = Encoding.UTF8.GetBytes(resultJson);
                    _logger.LogInformation("Sending OCR + Summary JSON: " + resultJson);

                    _channel.BasicPublish(
                        exchange: "",
                        routingKey: _resultQueue,
                        basicProperties: null,
                        body: body
                    );

                    _logger.LogInformation("Sent OCR + Summary result for DocumentId: {Id}", message.DocumentId);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid JSON format in received message.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while processing message.");
                }
            };

            _channel.BasicConsume(queue: _requestQueue, autoAck: true, consumer: consumer);
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _channel?.Close();
                _connection?.Close();
                _logger.LogInformation("RabbitMQ connection closed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing RabbitMQ connection.");
            }

            return base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            try
            {
                _channel?.Dispose();
                _connection?.Dispose();
                _logger.LogInformation("RabbitMQ resources disposed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while disposing RabbitMQ resources.");
            }

            base.Dispose();
        }
    }
}
