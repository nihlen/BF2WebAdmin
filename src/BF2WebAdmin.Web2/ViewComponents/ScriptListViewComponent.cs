using System.Threading.Tasks;
using BF2WebAdmin.DAL.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace BF2WebAdmin.Web.ViewComponents
{
    public class ScriptListViewComponent : ViewComponent
    {
        private readonly IScriptRepository _scriptRepository;

        public ScriptListViewComponent(IScriptRepository scriptRepository)
        {
            _scriptRepository = scriptRepository;
        }

        public IViewComponentResult Invoke()
        {
            var model = _scriptRepository.Get();
            return View(model);
        }
    }
}