using Microsoft.AspNetCore.Mvc;

namespace BF2WebAdmin.Web.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}