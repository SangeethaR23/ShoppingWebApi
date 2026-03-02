using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ShoppingWebApi.Models.Errors;

namespace ShoppingWebApi.Filters
{
    public class ValidateModelAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value" : e.ErrorMessage).ToArray()
                    );

                var problem = new ApiError
                {
                    Type = "https://httpstatuses.com/400",
                    Title = "Validation failed",
                    Status = 400,
                    Detail = "One or more validation errors occurred.",
                    Errors = errors
                };

                context.Result = new BadRequestObjectResult(problem);
            }
        }
    }
}