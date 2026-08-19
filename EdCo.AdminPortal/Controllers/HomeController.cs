using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using EdCo.AdminPortal.Models;
using Microsoft.AspNetCore.Authorization;

namespace EdCo.AdminPortal.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [AllowAnonymous]
    [Route("Privacy")]
    [Route("Home/Privacy")]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [Route("Terms")]
    [Route("Home/Terms")]
    public IActionResult Terms()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [Route("Home/Error/{id?}")]
    public IActionResult Error(int? id)
    {
        var code = id ?? HttpContext.Response.StatusCode;
        if (code == 200 || code == 0) code = 500;

        var model = new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = code
        };

        switch (code)
        {
            case 404:
                model.Title = "Page Not Found (404)";
                model.Description = "The requested page or resource could not be found or may have been moved.";
                break;
            case 403:
                model.Title = "Access Denied (403)";
                model.Description = "You do not have permission to access this resource or administrative area.";
                break;
            case 401:
                model.Title = "Unauthorized (401)";
                model.Description = "Your session may have expired. Please log in to continue.";
                break;
            case 500:
                model.Title = "Internal Server Error (500)";
                model.Description = "An unexpected server error occurred. Our team has been notified.";
                break;
            default:
                model.Title = $"Error ({code})";
                model.Description = "An unexpected HTTP error occurred while processing your request.";
                break;
        }

        return View(model);
    }
}
