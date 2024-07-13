// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using UnityEngine;

namespace VirtualMaker.RPC
{
    public class UnityRpcTransportNone : IUnityRpcTransport
    {
        public ConcurrentQueue<string> ReceiveQueue { get; } = new();

        public void SendMessage(string message)
        {
        }

        private void OnMessageInstance(string message)
        {
        }
    }
}
