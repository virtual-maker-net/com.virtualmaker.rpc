// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_WEBGL

using AOT;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace VirtualMaker.RPC
{
    public class UnityRpcTransportWebGL : IUnityRpcTransport
    {
        private static UnityRpcTransportWebGL _instance;

        public ConcurrentQueue<string> ReceiveQueue { get; }

        [DllImport("__Internal")]
        private static extern void UnityRpcTransportWebGLCreate(Action<string> onMessage);

        [DllImport("__Internal")]
        private static extern void UnityRpcTransportWebGLSend(string message);

        public UnityRpcTransportWebGL()
        {
            _instance = this;
            ReceiveQueue = new ConcurrentQueue<string>();
            UnityRpcTransportWebGLCreate(OnMessage);
        }

        [MonoPInvokeCallback(typeof(Action<string>))]
        public static void OnMessage(string message)
        {
            _instance.OnMessageInstance(message);
        }

        public void SendMessage(string message)
        {
            UnityRpcLog.Info($"[UnityRpcTransportWebGL] Send: {message}");
            UnityRpcTransportWebGLSend(message);
        }

        private void OnMessageInstance(string message)
        {
            UnityRpcLog.Info($"[UnityRpcTransportWebGL] Receive: {message}");
            ReceiveQueue.Enqueue(message);
        }
    }
}

#endif // UNITY_WEBGL
