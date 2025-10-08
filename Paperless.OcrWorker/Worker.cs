using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paperless.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Paperless.OcrWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private IConnection? _connection;
        private IModel? _channel;
        private string _requestQueue = "document_queue";
        private string _resultQueue = "result_queue";

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
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

            _logger.LogInformation("OCR Worker is connected to RabbitMQ at {Host}", host);
            return base.StartAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null)
                throw new InvalidOperationException("The channel not initialized.");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (_, ea) =>
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<OcrMessage>(json);

                if (message == null)
                {
                    _logger.LogWarning("Invalid message.");
                    return;
                }

                _logger.LogInformation("Received OCR job for DocumentId: {Id}", message.DocumentId);

                await Task.Delay(1500, stoppingToken);
                message.OcrText = "OCR result: Lorem ipsum blabla irgendwas fake result blablabla";

                var resultJson = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(resultJson);

                _channel.BasicPublish(
                    exchange: "",
                    routingKey: _resultQueue,
                    basicProperties: null,
                    body: body
                );

                _logger.LogInformation("Sent OCR result for DocumentId: {Id}", message.DocumentId);
            };

            _channel.BasicConsume(queue: _requestQueue, autoAck: true, consumer: consumer);
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
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}
