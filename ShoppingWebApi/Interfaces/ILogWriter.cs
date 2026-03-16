namespace ShoppingWebApi.Interfaces
{
    public interface ILogWriter
    {
        Task InfoAsync(
            string source,
            string message,
            int? eventId = null,
            string? correlationId = null,
            string? requestPath = null,
            CancellationToken ct = default);

        Task WarnAsync(
            string source,
            string message,
            int? eventId = null,
            string? correlationId = null,
            string? requestPath = null,
            CancellationToken ct = default);

        Task ErrorAsync(
            string source,
            string message,
            Exception? ex = null,
            int? eventId = null,
            string? correlationId = null,
            string? requestPath = null,
            CancellationToken ct = default);
    }
}