using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Common.Abstractions;
using BF2WebAdmin.Common.Communication;
using BF2WebAdmin.Common.Communication.Messages;
using BF2WebAdmin.Web.Extensions;
using Microsoft.AspNetCore.Http;

namespace BF2WebAdmin.Web
{
    public class WebSocketServer
    {
        // https://medium.com/@turowicz/websockets-in-asp-net-5-6094319a15a2#.2hngjfb78
        private readonly ConcurrentBag<WebSocket> _webSockets;
        private readonly ConcurrentDictionary<Guid, WebSocket> _userConnections;
        private readonly IMessageSerializer _textSerializer;
        private readonly IMessageSerializer _binarySerializer;
        private readonly IMessageQueue<MessageAction, MessageEvent> _messageQueue;

        public WebSocketServer()
        {
            _webSockets = new ConcurrentBag<WebSocket>();
            _userConnections = new ConcurrentDictionary<Guid, WebSocket>();
            _textSerializer = new JsonSerializer();
            //_binarySerializer = new MsgPackSerializer();
            _binarySerializer = new ProtobufSerializer(@"C:\Projects\DotNet\BF2WebAdmin\src\BF2WebAdmin.Web\App\messages.proto");
            _messageQueue = new MessageQueue<MessageAction, MessageEvent>("@tcp://localhost:6006");
            _messageQueue.Receive += (sender, args) => HandleEvent(args.Message);
        }

        private async void HandleEvent(MessageEvent message)
        {
            var tasks = new List<Task>(_userConnections.Count);

            // Text
            var bytes = _textSerializer.Serialize(message);
            var json = Encoding.UTF8.GetString(bytes);
            foreach (var userConnection in _userConnections.Values)
            {
                tasks.Add(userConnection.SendTextAsync(json));
            }

            // Binary
            //var bytes = _binarySerializer.Serialize(message);
            //foreach (var userConnection in _userConnections.Values)
            //{
            //    var task = userConnection.SendBinaryAsync(bytes);
            //}

            await Task.WhenAll(tasks);
        }

        public async Task HandleWebSockets(HttpContext http, Func<Task> next)
        {
            if (http.WebSockets.IsWebSocketRequest)
            {
                await AwaitConnection(http);
            }
            else
            {
                // Nothing to do here, pass downstream
                await next();
            }
        }

        private async Task AwaitConnection(HttpContext http)
        {
            var webSocket = await http.WebSockets.AcceptWebSocketAsync();
            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                _webSockets.Add(webSocket);
                var playerId = PlayerConnect(webSocket);
                while (webSocket.State == WebSocketState.Open)
                {
                    await AwaitMessage(webSocket);
                }
                PlayerDisconnect(playerId);
            }
        }

        private async Task AwaitMessage(WebSocket webSocket)
        {
            var token = CancellationToken.None;
            var buffer = new ArraySegment<byte>(new byte[4096]);

            // Below will wait for a request message
            var received = await webSocket.ReceiveAsync(buffer, token);

            switch (received.MessageType)
            {
                case WebSocketMessageType.Text:
                    var textBytes = new byte[received.Count];
                    Array.Copy(buffer.Array, buffer.Offset, textBytes, 0, received.Count);
                    _messageQueue.Send(_textSerializer.Deserialize<MessageAction>(textBytes));
                    break;

                case WebSocketMessageType.Binary:
                    var binaryBytes = new byte[received.Count];
                    Array.Copy(buffer.Array, buffer.Offset, binaryBytes, 0, received.Count);
                    _messageQueue.Send(_binarySerializer.Deserialize<MessageAction>(binaryBytes));
                    break;

                case WebSocketMessageType.Close:
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private Guid PlayerConnect(WebSocket webSocket)
        {
            // TODO: ServerId ?
            var userId = Guid.NewGuid();
            _userConnections.AddOrUpdate(userId, webSocket, (guid, socket) => socket);
            _messageQueue.Send(MessageAction.Create("?", new UserConnectAction
            {
                Id = userId.ToString()
            }));
            return userId;
        }

        private void PlayerDisconnect(Guid playerId)
        {
            // TODO: ServerId ?
            var removed = _userConnections.TryRemove(playerId, out var removedWebSocket);
            _messageQueue.Send(MessageAction.Create("?", new UserDisconnectAction
            {
                Id = playerId.ToString()
            }));
        }

        //public async Task HandleWebSockets(HttpContext http, Func<Task> next)
        //{
        //    if (http.WebSockets.IsWebSocketRequest)
        //    {
        //        await AwaitConnection(http);
        //    }
        //    else
        //    {
        //        // Nothing to do here, pass downstream.  
        //        await next();
        //    }
        //}

        //private async Task AwaitConnection(HttpContext http)
        //{
        //    var webSocket = await http.WebSockets.AcceptWebSocketAsync();
        //    if (webSocket != null && webSocket.State == WebSocketState.Open)
        //    {
        //        _sockets.Add(webSocket);
        //        await webSocket.SendTextAsync("Connected!");
        //        while (webSocket.State == WebSocketState.Open)
        //        {
        //            await AwaitMessage(webSocket);
        //        }
        //    }
        //}

        //private async Task AwaitMessage(WebSocket webSocket)
        //{
        //    var token = CancellationToken.None;
        //    var buffer = new ArraySegment<byte>(new byte[4096]);

        //    // Below will wait for a request message.
        //    var received = await webSocket.ReceiveAsync(buffer, token);

        //    switch (received.MessageType)
        //    {
        //        case WebSocketMessageType.Text:
        //            var request = Encoding.UTF8.GetString(buffer.Array,
        //                buffer.Offset,
        //                buffer.Count);
        //            // Handle request here.
        //            await webSocket.SendTextAsync($"Received: {request}");
        //            break;
        //        case WebSocketMessageType.Binary:
        //            break;
        //        case WebSocketMessageType.Close:
        //            break;
        //        default:
        //            throw new ArgumentOutOfRangeException();
        //    }
        //}
    }
}