namespace Paperless.DAL.Service.Exceptions
{
    public class DatabaseConnectionException : RepositoryException
    {
        public DatabaseConnectionException() { }
        public DatabaseConnectionException(string message) : base(message) { }
        public DatabaseConnectionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
