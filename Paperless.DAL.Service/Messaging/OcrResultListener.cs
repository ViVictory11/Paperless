using System.Text;
using System.Text.Json;
using Paperless.Contracts;
using Paperless.DAL.Service.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paperless.DAL.Service.Messaging
{
    public class OcrResultListener : BackgroundService
    {
        private readonly IOcrResult _resultStore;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly string _resultQueue = "result_queue";

        public OcrResultListener(IOcrResult resultStore)
        {
            _resultStore = resultStore;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
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

            return base.StartAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null)
                throw new InvalidOperationException("Channel not initialized.");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<OcrMessage>(json);

                if (message != null && !string.IsNullOrEmpty(message.OcrText))
                {
                    _resultStore.SaveResult(message.DocumentId, message.OcrText);
                    Console.WriteLine($"Stored result for document {message.DocumentId}");
                }
                else
                {
                    Console.WriteLine("Invalid or empty OCR result.");
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
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}
