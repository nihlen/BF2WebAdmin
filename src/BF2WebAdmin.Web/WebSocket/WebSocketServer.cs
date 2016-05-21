using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;

namespace BF2WebAdmin.Web
{
    public class WebSocketServer
    {
        // https://medium.com/@turowicz/websockets-in-asp-net-5-6094319a15a2#.2hngjfb78
        private readonly ConcurrentBag<WebSocket> _sockets;

        public WebSocketServer()
        {
            _sockets = new ConcurrentBag<WebSocket>();
        }

        public async Task HandleWebSockets(HttpContext http, Func<Task> next)
        {
            if (http.WebSockets.IsWebSocketRequest)
            {
                await AwaitConnection(http);
            }
            else
            {
                // Nothing to do here, pass downstream.  
                await next();
            }
        }

        private async Task AwaitConnection(HttpContext http)
        {
            var webSocket = await http.WebSockets.AcceptWebSocketAsync();
            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                _sockets.Add(webSocket);
                await webSocket.SendText("Connected!");
                while (webSocket.State == WebSocketState.Open)
                {
                    await AwaitMessage(webSocket);
                }
            }
        }

        private async Task AwaitMessage(WebSocket webSocket)
        {
            var token = CancellationToken.None;
            var buffer = new ArraySegment<byte>(new byte[4096]);

            // Below will wait for a request message.
            var received = await webSocket.ReceiveAsync(buffer, token);

            switch (received.MessageType)
            {
                case WebSocketMessageType.Text:
                    var request = Encoding.UTF8.GetString(buffer.Array,
                        buffer.Offset,
                        buffer.Count);
                    // Handle request here.
                    await webSocket.SendText($"Received: {request}");
                    break;
                case WebSocketMessageType.Binary:
                    break;
                case WebSocketMessageType.Close:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}