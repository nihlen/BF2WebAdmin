using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace BF2WebAdmin.Web
{
    public interface IWebSocketServer
    {
        Task HandleWebSockets(HttpContext http, Func<Task> next);
    }
}