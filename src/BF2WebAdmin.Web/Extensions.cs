using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BF2WebAdmin.Web
{
    public static class Extensions
    {
        public static Task SendText(this WebSocket webSocket, string text)
        {
            var token = CancellationToken.None;
            var message = new ArraySegment<byte>(Encoding.UTF8.GetBytes(text));
            return webSocket.SendAsync(message, WebSocketMessageType.Text, true, token);
        }
    }
}
