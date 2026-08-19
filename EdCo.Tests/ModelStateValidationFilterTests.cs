using EdCo.Core.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace EdCo.Tests
{
    public class ModelStateValidationFilterTests
    {
        [Fact]
        public void OnActionExecuting_ValidModelState_DoesNotSetResult()
        {
            // Arrange
            var filter = new ModelStateValidationFilter();
            var modelState = new ModelStateDictionary();
            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), modelState);
            var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

            // Act
            filter.OnActionExecuting(context);

            // Assert
            Assert.Null(context.Result);
        }

        [Fact]
        public void OnActionExecuting_InvalidModelState_ReturnsBadRequestProblemDetails()
        {
            // Arrange
            var filter = new ModelStateValidationFilter();
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Email", "The Email field is required.");
            
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/api/v1/auth/login";

            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), modelState);
            var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

            // Act
            filter.OnActionExecuting(context);

            // Assert
            Assert.NotNull(context.Result);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(context.Result);
            var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);

            Assert.Equal(400, problemDetails.Status);
            Assert.Equal("/api/v1/auth/login", problemDetails.Instance);
            Assert.True(problemDetails.Errors.ContainsKey("Email"));
            Assert.Contains("The Email field is required.", problemDetails.Errors["Email"]);
        }
    }
}
