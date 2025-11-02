namespace Paperless.DAL.Service.Exceptions
{
    public class StorageException : RepositoryException
    {
        public StorageException() { }

        public StorageException(string message) : base(message) { }

        public StorageException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
