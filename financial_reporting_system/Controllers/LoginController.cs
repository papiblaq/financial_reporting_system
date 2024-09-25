using Microsoft.AspNetCore.Mvc;

namespace financial_reporting_system.Controllers
{
    public class LoginController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
