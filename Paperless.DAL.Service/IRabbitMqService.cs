namespace Paperless.DAL.Service
{
    public interface IRabbitMqService
    {
        void SendMessage(string message);
    }
}
