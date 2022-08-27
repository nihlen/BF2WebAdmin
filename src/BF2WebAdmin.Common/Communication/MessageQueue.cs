using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BF2WebAdmin.Common.Abstractions;
using NetMQ;
using NetMQ.Sockets;

namespace BF2WebAdmin.Common.Communication;

/// <summary>
/// A wrapper for NetMQ connections
/// </summary>
/// <typeparam name="TReceive">The type of message that comes in from the remote connection (ServerMessage)</typeparam>
/// <typeparam name="TSend">The type of message that is sent from the local connection (ClientMessage)</typeparam>
public class MessageQueue<TSend, TReceive> : IMessageQueue<TSend, TReceive>, IDisposable
{
    private const int MaxQueueCapacity = 100;

    private readonly IMessageSerializer _serializer;
    private readonly string _connectionString;
    private readonly ConcurrentQueue<TReceive> _receiveBuffer;
    private NetMQSocket _serverSocket;
    private NetMQQueue<byte[]> _sendQueue;

    public event EventHandler<MessageEventArgs<TReceive>> Receive;

    public MessageQueue(string connectionString)
    {
        _serializer = new ProtobufSerializer();
        _connectionString = connectionString;
        _receiveBuffer = new ConcurrentQueue<TReceive>();

        Listen();
    }

    private void Listen()
    {
        try
        {
            // TODO: look into NetMQRuntime for async/await?
            _serverSocket = new PairSocket(_connectionString);
            _serverSocket.ReceiveReady += OnReceive;

            _sendQueue = new NetMQQueue<byte[]>(MaxQueueCapacity);
            _sendQueue.ReceiveReady += OnSend;

            // Start the poller which alternates between listening and sending
            var poller = new NetMQPoller { _serverSocket, _sendQueue };
            poller.RunAsync();
        }
        catch (Exception ex)
        {
            //Logger.LogError("Game listener crashed", ex);
            throw;
        }
    }

    private void OnReceive(object sender, NetMQSocketEventArgs args)
    {
        // Send to listeners if we have any, else store message
        while (args.Socket.TryReceiveFrameBytes(out var frameBuffer))
        {
            var message = _serializer.Deserialize<TReceive>(frameBuffer);

            var hasListeners = Receive != null && Receive.GetInvocationList().Any();
            if (hasListeners)
                Receive(this, new MessageEventArgs<TReceive> { Message = message });
            else
                _receiveBuffer.Enqueue(message);
        }
    }

    private void OnSend(object sender, NetMQQueueEventArgs<byte[]> args)
    {
        var message = args.Queue.Dequeue();
        _serverSocket.SendFrame(message);
    }

    public IEnumerable<TReceive> ReceiveAll()
    {
        while (_receiveBuffer.TryDequeue(out var result))
        {
            yield return result;
        }
    }

    public void Send(TSend message)
    {
        // Queue may be full, but we can't empty it. Return to avoid blocking.
        if (_sendQueue == null)
            return;
        if (_sendQueue.Count > MaxQueueCapacity - 100)
            return;

        var data = _serializer.Serialize(message);
        _sendQueue.Enqueue(data);
    }

    public void Dispose()
    {
        _sendQueue?.Dispose();
        _serverSocket?.Dispose();
    }
}

public class MessageEventArgs<TReceive> : EventArgs
{
    public TReceive Message { get; set; }
}