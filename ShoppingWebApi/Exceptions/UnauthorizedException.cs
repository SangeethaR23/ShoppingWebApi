namespace ShoppingWebApi.Exceptions
{

    public class UnauthorizedAppException : Exception
    {
        public UnauthorizedAppException(string message) : base(message) { }

    }
}
