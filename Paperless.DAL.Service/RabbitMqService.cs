using Microsoft.AspNetCore.Connections;
using Paperless.DAL.Service;
using RabbitMQ.Client;
using System.Text;

public class RabbitMqService : IRabbitMqService
{
    private readonly IConnection _connection;
    private readonly RabbitMQ.Client.IModel _channel;

    public RabbitMqService()
    {
        var factory = new ConnectionFactory()
        {
            HostName = "rabbitmq",
            UserName = "user",
            Password = "pass"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
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
        var body = Encoding.UTF8.GetBytes(message);
        _channel.BasicPublish(
            exchange: "",
            routingKey: "document_queue",
            basicProperties: null,
            body: body
        );
    }
}
