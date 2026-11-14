namespace GlobalExceptionHandling.GlobalExceptionHandlers.Exceptions
{
    public class BadRequestException : Exception
    {
        public BadRequestException(string message) : base(message)
        {
        }
    }
}