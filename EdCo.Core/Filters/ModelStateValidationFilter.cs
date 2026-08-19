using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EdCo.Core.Filters
{
    /// <summary>
    /// Action filter that intercepts invalid model states and returns a standardized RFC-7807 ValidationProblemDetails response.
    /// </summary>
    public class ModelStateValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var problemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "One or more validation errors occurred.",
                    Status = 400,
                    Detail = "Please refer to the errors property for additional details.",
                    Instance = context.HttpContext.Request.Path
                };

                context.Result = new BadRequestObjectResult(problemDetails)
                {
                    ContentTypes = { "application/problem+json", "application/json" }
                };
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // No action needed post-execution
        }
    }
}
