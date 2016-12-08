using System;
using BF2WebAdmin.DAL.Abstractions;
using BF2WebAdmin.DAL.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BF2WebAdmin.Web.Controllers
{
    public class ManageController : Controller
    {
        private readonly IScriptRepository _scriptRepository;
        private readonly IMapRepository _mapRepository;
        private readonly ILogger<HomeController> _logger;

        public ManageController(IScriptRepository scriptRepository, IMapRepository mapRepository, ILogger<HomeController> logger)
        {
            _scriptRepository = scriptRepository;
            _mapRepository = mapRepository;
            _logger = logger;
        }

        //public IActionResult Index()
        //{
        //    return View();
        //}

        public IActionResult Scripts()
        {
            var model = _scriptRepository.Get();
            return View(model);
        }

        public IActionResult AddScript()
        {
            return PartialView(new GameScript());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddScript(GameScript script)
        {
            try
            {
                script.Id = Guid.NewGuid();
                _scriptRepository.Create(script);
                SuccessMessage($"Script {script.Id} added");
            }
            catch (Exception ex)
            {
                ErrorMessage("Error while adding script", ex);
            }
            return RedirectToAction("Scripts");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveScript(Guid id)
        {
            try
            {
                _scriptRepository.Delete(id);
                SuccessMessage($"Script {id} deleted");
            }
            catch (Exception ex)
            {
                ErrorMessage("Error while removing script", ex);
            }
            return RedirectToAction("Scripts");
        }

        private void SuccessMessage(string message)
        {
            ViewData["Success"] = message;
        }

        private void ErrorMessage(string message, Exception ex = null)
        {
            ViewData["Success"] = message;
            if (ex != null)
                _logger.LogError(message, ex);
        }
    }
}
