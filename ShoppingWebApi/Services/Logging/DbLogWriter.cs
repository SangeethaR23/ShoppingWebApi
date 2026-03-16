using System;
using System.Threading;
using System.Threading.Tasks;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Interfaces;

namespace ShoppingWebApi.Services.Logging
{
    public class DbLogWriter : ILogWriter
    {
        private readonly AppDbContext _db;
        public DbLogWriter(AppDbContext db) => _db = db;

        public Task InfoAsync(string source, string message, int? eventId = null, string? correlationId = null, string? requestPath = null, CancellationToken ct = default)
            => WriteAsync("Info", source, message, null, eventId, correlationId, requestPath, ct);

        public Task WarnAsync(string source, string message, int? eventId = null, string? correlationId = null, string? requestPath = null, CancellationToken ct = default)
            => WriteAsync("Warn", source, message, null, eventId, correlationId, requestPath, ct);

        public Task ErrorAsync(string source, string message, Exception? ex = null, int? eventId = null, string? correlationId = null, string? requestPath = null, CancellationToken ct = default)
            => WriteAsync("Error", source, message, ex, eventId, correlationId, requestPath, ct);

        private async Task WriteAsync(
            string level,
            string source,
            string message,
            Exception? ex,
            int? eventId,
            string? correlationId,
            string? requestPath,
            CancellationToken ct)
        {
            try
            {
                var log = new ShoppingWebApi.Models.LogEntry
                {
                    Level = level,
                    Message = message,
                    Exception = ex?.Message,
                    StackTrace = ex?.ToString(),
                    Source = source,
                    EventId = eventId,
                    CorrelationId = correlationId,
                    RequestPath = requestPath,
                    // BaseEntity likely has Id, CreatedUtc/UpdatedUtc handled via interceptor or on save
                };

                _db.Logs.Add(log);
                await _db.SaveChangesAsync(ct);
            }
            catch
            {
           
            }
        }
    }
}