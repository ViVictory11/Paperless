using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Paperless.OcrWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private IConnection? _connection;
        private IModel? _channel;
        private string _queueName = "document_queue";

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
            var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "user";
            var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "pass";
            _queueName = Environment.GetEnvironmentVariable("RABBITMQ_QUEUE") ?? "document_queue";

            var factory = new ConnectionFactory
            {
                HostName = host,
                UserName = user,
                Password = pass
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: _queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

            _logger.LogInformation("OCR Worker connected to RabbitMQ at {Host}, queue {Queue}", host, _queueName);
            return base.StartAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null) throw new InvalidOperationException("Channel not initialized.");
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (_, ea) =>
            {
                var msg = Encoding.UTF8.GetString(ea.Body.ToArray());
                _logger.LogInformation("?? OCR Worker received: {Message}", msg);
                // TODO: OCR pipeline (empty for now)
            };

            _channel.BasicConsume(queue: _queueName, autoAck: true, consumer: consumer);
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
