using System;
using System.Collections.Generic;
using BF2WebAdmin.Common.Communication;

namespace BF2WebAdmin.Common.Abstractions;

public interface IMessageQueue<in TSend, TReceive>
{
    event EventHandler<MessageEventArgs<TReceive>> Receive;
    IEnumerable<TReceive> ReceiveAll();
    void Send(TSend message);
    void Dispose();
}