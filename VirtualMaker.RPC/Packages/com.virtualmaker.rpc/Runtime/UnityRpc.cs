// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utilities.Async;

namespace VirtualMaker.RPC
{
    /// <summary>
    /// This class lets you communicate between Unity and the browser using RPC over JSON.
    /// Technically it could let you communicate with any remote source by implementing
    /// <see cref="IUnityRpcTransport"/>.
    /// </summary>
    public class UnityRpc : IDisposable
    {
        private struct Message
        {
            [JsonProperty("event")]
            public string Event { get; set; }

            [JsonProperty("args")]
            public JToken[] Args { get; set; }

            [JsonProperty("rpc-id")]
            public int? RpcId { get; set; }

            [JsonProperty("response-id")]
            public int? ResponseId { get; set; }
        }

        private int _rpcId = 1;

        private Dictionary<int, Action<JToken>> _rpcs = new();

        private Dictionary<string, List<Delegate>> _subscriptions = new();

        private IUnityRpcTransport _transport;

        private CancellationTokenSource _cancellationTokenSource;

        public UnityRpc(IUnityRpcTransport transport)
        {
            _transport = transport;
            _cancellationTokenSource = new CancellationTokenSource();
            ProcessQueue(_cancellationTokenSource.Token);
        }

        public Task<T> CallAsync<T>(string evt, params object[] args)
        {
            var message = new Message
            {
                Event = evt,
                Args = args.Select(JToken.FromObject).ToArray(),
                RpcId = _rpcId++
            };

            var tcs = new TaskCompletionSource<T>();
            _rpcs.Add(message.RpcId.Value, result =>
            {
                tcs.SetResult(result.ToObject<T>());
            });

            var json = JsonConvert.SerializeObject(message);
            _transport.SendMessage(json);
            return tcs.Task;
        }

        public void RaiseEvent(string evt, params object[] args)
        {
            var message = new Message
            {
                Event = evt,
                Args = args.Select(JToken.FromObject).ToArray()
            };

            var json = JsonConvert.SerializeObject(message);
            _transport.SendMessage(json);
        }

        public void SubscribeDelegate(string evt, Delegate callback)
        {
            if (!_subscriptions.TryGetValue(evt, out var callbacks))
            {
                callbacks = new List<Delegate>();
                _subscriptions.Add(evt, callbacks);
            }

            callbacks.Add(callback);
        }

        public void Subscribe<T>(string evt, Action<T> callback)
        {
            SubscribeDelegate(evt, callback);
        }

        public void Subscribe<T1, T2>(string evt, Action<T1, T2> callback)
        {
            SubscribeDelegate(evt, callback);
        }

        public void Subscribe<T1, T2, T3>(string evt, Action<T1, T2, T3> callback)
        {
            SubscribeDelegate(evt, callback);
        }

        public void CreateRpc<T, TResult>(string evt, Func<T, TResult> callback)
        {
            SubscribeDelegate(evt, callback);
        }

        public void CreateRpc<T1, T2, TResult>(string evt, Func<T1, T2, TResult> callback)
        {
            SubscribeDelegate(evt, callback);
        }

        public void CreateRpc<T1, T2, T3, TResult>(string evt, Func<T1, T2, T3, TResult> callback)
        {
            SubscribeDelegate(evt, callback);
        }

        public void UnsubscribeDelegate(string evt, Delegate callback)
        {
            if (_subscriptions == null)
            {
                return;
            }

            if (!_subscriptions.TryGetValue(evt, out var callbacks))
            {
                return;
            }

            callbacks.Remove(callback);
        }

        public void Unsubscribe<T>(string evt, Action<T> callback)
        {
            UnsubscribeDelegate(evt, callback);
        }

        public void Unsubscribe<T1, T2>(string evt, Action<T1, T2> callback)
        {
            UnsubscribeDelegate(evt, callback);
        }

        public void Unsubscribe<T1, T2, T3>(string evt, Action<T1, T2, T3> callback)
        {
            UnsubscribeDelegate(evt, callback);
        }

        public void RemoveRpc<T, TResult>(string evt, Func<T, TResult> callback)
        {
            UnsubscribeDelegate(evt, callback);
        }

        public void RemoveRpc<T1, T2, TResult>(string evt, Func<T1, T2, TResult> callback)
        {
            UnsubscribeDelegate(evt, callback);
        }

        public void RemoveRpc<T1, T2, T3, TResult>(string evt, Func<T1, T2, T3, TResult> callback)
        {
            UnsubscribeDelegate(evt, callback);
        }

        private async void ProcessQueue(CancellationToken cancellationToken)
        {
            while (_transport.ReceiveQueue.TryDequeue(out var message) &&
                   !cancellationToken.IsCancellationRequested)
            {
                await Awaiters.UnityMainThread;

                try
                {
                    var parsed = JsonConvert.DeserializeObject<Message>(message);

                    if (parsed.ResponseId.HasValue)
                    {
                        if (_rpcs == null || !_rpcs.Remove(parsed.ResponseId.Value, out var callback))
                        {
                            continue;
                        }

                        callback(parsed.Args[0]);
                        continue;
                    }

                    if (_subscriptions == null || !_subscriptions.TryGetValue(parsed.Event, out var callbacks))
                    {
                        continue;
                    }

                    foreach (var callback in callbacks)
                    {
                        try
                        {
                            var method = callback.Method;
                            var parameters = method.GetParameters();
                            var args = new object[parameters.Length];

                            for (var i = 0; i < parameters.Length; i++)
                            {
                                var parameter = parameters[i];
                                var arg = parsed.Args[i];
                                args[i] = arg.ToObject(parameter.ParameterType);
                            }

                            var result = callback.DynamicInvoke(args);

                            if (result == null) { continue; }

                            var response = new Message
                            {
                                Event = parsed.Event,
                                Args = new[] { JToken.FromObject(result) },
                                ResponseId = parsed.RpcId
                            };

                            var json = JsonConvert.SerializeObject(response);
                            _transport.SendMessage(json);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private void CancelProcessQueue()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        public void Dispose()
        {
            CancelProcessQueue();
            GC.SuppressFinalize(this);
        }

        ~UnityRpc()
        {
            CancelProcessQueue();
        }
    }
}
