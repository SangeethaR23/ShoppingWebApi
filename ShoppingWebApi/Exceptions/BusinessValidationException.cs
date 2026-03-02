namespace ShoppingWebApi.Exceptions
{
    public class BusinessValidationException : Exception
    {
        public IDictionary<string, string[]> Errors { get; }

        public BusinessValidationException(string message, IDictionary<string, string[]>? errors = null)
            : base(message)
        {
            Errors = errors ?? new Dictionary<string, string[]>();
        }
    }
}