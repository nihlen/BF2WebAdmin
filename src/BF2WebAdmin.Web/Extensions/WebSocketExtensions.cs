using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BF2WebAdmin.Web.Extensions
{
    public static class WebSocketExtensions
    {
        public static async Task SendTextAsync(this WebSocket webSocket, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var ar = new ArraySegment<byte>(bytes);
            if (webSocket.State != WebSocketState.Open)
                return;

            await webSocket.SendAsync(ar, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public static async Task SendBinaryAsync(this WebSocket webSocket, byte[] bytes)
        {
            var ar = new ArraySegment<byte>(bytes);
            if (webSocket.State != WebSocketState.Open)
                return;

            await webSocket.SendAsync(ar, WebSocketMessageType.Binary, true, CancellationToken.None);
        }
    }
}