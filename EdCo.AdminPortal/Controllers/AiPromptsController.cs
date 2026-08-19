using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AiPromptsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
