using Microsoft.AspNetCore.Mvc;

namespace financial_reporting_system.Controllers
{
    public class Menu : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
