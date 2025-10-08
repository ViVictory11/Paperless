using Microsoft.AspNetCore.Connections;
using Paperless.DAL.Service;
using RabbitMQ.Client;
using System.Text;

public class RabbitMqService : IRabbitMqService
{
    private readonly IModel _channel;

    public RabbitMqService()
    {
        var factory = new ConnectionFactory()
        {
            HostName = "rabbitmq",
            UserName = "user",
            Password = "pass"
        };

        var connection = factory.CreateConnection();
        _channel = connection.CreateModel();

        _channel.QueueDeclare(
            queue: "document_queue",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );
    }

    public void SendMessage(string message)
    {
        try
        {
            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(
                exchange: "",
                routingKey: "document_queue",
                basicProperties: null,
                body: body
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to send message: {ex.Message}");
        }
    }
}
