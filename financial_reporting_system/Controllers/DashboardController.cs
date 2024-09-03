using Microsoft.AspNetCore.Mvc;

namespace financial_reporting_system.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
