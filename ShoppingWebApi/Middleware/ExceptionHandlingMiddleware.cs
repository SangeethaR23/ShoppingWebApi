using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Exceptions;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.Errors;

namespace ShoppingWebApi.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context, AppDbContext db)
        {
            string correlationId = context.TraceIdentifier;

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // Map exception to status code + title
                var (status, title, errors) = MapException(ex);

                // Log to ILogger
                if (status >= 500)
                    _logger.LogError(ex, "[{CorrelationId}] {Title}", correlationId, title);
                else
                    _logger.LogWarning(ex, "[{CorrelationId}] {Title}", correlationId, title);

                // Persist to LogEntry table (best-effort, don’t throw if logging fails)
                try
                {
                    var log = new LogEntry
                    {
                        Level = status >= 500 ? "Error" : "Warning",
                        Message = ex.Message,
                        Exception = ex.GetType().Name,
                        StackTrace = ex.StackTrace,
                        Source = ex.TargetSite?.DeclaringType?.Name,
                        EventId = null,
                        CorrelationId = correlationId,
                        RequestPath = context.Request?.Path.Value
                    };
                    db.Logs.Add(log);
                    await db.SaveChangesAsync();
                }
                catch { /* swallow logging failures */ }

                // Build ProblemDetails response
                var problem = new ApiError
                {
                    Type = $"https://httpstatuses.com/{status}",
                    Title = title,
                    Status = status,
                    Detail = ex.Message,
                    Instance = context.Request?.Path.Value,
                    CorrelationId = correlationId,
                    Errors = errors
                };

                context.Response.ContentType = "application/problem+json";
                context.Response.StatusCode = status;

                // Use system text json to avoid circular refs
                var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                await context.Response.WriteAsync(json);
            }
        }

        private static (int status, string title, IDictionary<string, string[]>? errors) MapException(Exception ex)
        {
            return ex switch
            {
                NotFoundException => ((int)HttpStatusCode.NotFound, "Resource not found", null),
                ConflictException => ((int)HttpStatusCode.Conflict, "Conflict", null),
                ForbiddenException => ((int)HttpStatusCode.Forbidden, "Forbidden", null),
                UnauthorizedAppException => ((int)HttpStatusCode.Unauthorized, "Unauthorized", null),

                BusinessValidationException bvx => ((int)HttpStatusCode.BadRequest, "Validation failed", bvx.Errors),

                DbUpdateException => ((int)HttpStatusCode.Conflict, "Database update error", null),
                //DbUpdateConcurrencyException => ((int)HttpStatusCode.Conflict, "Concurrency conflict", null),

                _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred", null)
            };
        }
    }

    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}