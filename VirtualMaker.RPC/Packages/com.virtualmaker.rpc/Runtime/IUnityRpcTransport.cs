// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace VirtualMaker.RPC
{
    internal interface IUnityRpcTransport
    {
        ConcurrentQueue<string> ReceiveQueue { get; }
        void SendMessage(string message);
    }
}
