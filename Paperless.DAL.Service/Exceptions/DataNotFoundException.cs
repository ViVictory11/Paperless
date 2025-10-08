namespace Paperless.DAL.Service.Exceptions
{
    public class DataNotFoundException : RepositoryException
    {
        public DataNotFoundException() { }
        public DataNotFoundException(string message) : base(message) { }
        public DataNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
}
