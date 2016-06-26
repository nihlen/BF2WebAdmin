using System.Reflection;
using BF2WebAdmin.Server.Commands;
using log4net;
using log4net.Config;

namespace BF2WebAdmin.Server.Modules
{
    public class TestModule : IModule
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    }
}