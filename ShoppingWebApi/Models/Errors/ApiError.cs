using Microsoft.AspNetCore.Mvc;

namespace ShoppingWebApi.Models.Errors
{
    public class ApiError:ProblemDetails
    {

        public string? CorrelationId { get; set; }
        public IDictionary<string, string[]>? Errors { get; set; }

    }
}
