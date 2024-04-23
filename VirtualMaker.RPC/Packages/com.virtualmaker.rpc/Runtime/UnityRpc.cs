// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Utilities.Async;

namespace VirtualMaker.RPC
{
    /// <summary>
    /// This class lets you communicate between Unity and the browser using RPC over JSON.
    /// Technically it could let you communicate with any remote source by implementing
    /// IUnityRpcTransport.
    /// </summary>
    public static class UnityRpc
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

        private static IUnityRpcTransport _transport;

        private static Dictionary<string, List<Delegate>> _subscriptions = new();

        private static Dictionary<int, Action<JToken>> _rpcs = new();

        private static int _rpcId = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
#if UNITY_WEBGL
            if (Application.isEditor)
            {
                _transport = new UnityRpcTransportWebsocket();
            }
            else
            {
                _transport = new UnityRpcTransportWebGL();
            }
#else
            _transport = new UnityRpcTransportWebsocket();
#endif
            ProcessQueue();
        }

        public static Task<T> CallAsync<T>(string evt, params object[] args)
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

        public static void RaiseEvent(string evt, params object[] args)
        {
            var message = new Message
            {
                Event = evt,
                Args = args.Select(JToken.FromObject).ToArray()
            };

            var json = JsonConvert.SerializeObject(message);
            _transport.SendMessage(json);
        }

        public static void SubscribeDelegate(string evt, Delegate callback)
        {
            if (!_subscriptions.TryGetValue(evt, out var callbacks))
            {
                callbacks = new List<Delegate>();
                _subscriptions.Add(evt, callbacks);
            }

            callbacks.Add(callback);
        }

        public static void Subscribe<T>(string evt, Action<T> callback)
            => SubscribeDelegate(evt, callback);

        public static void Subscribe<T1, T2>(string evt, Action<T1, T2> callback)
            => SubscribeDelegate(evt, callback);

        public static void Subscribe<T1, T2, T3>(string evt, Action<T1, T2, T3> callback)
            => SubscribeDelegate(evt, callback);

        public static void CreateRpc<T, TResult>(string evt, Func<T, TResult> callback)
            => SubscribeDelegate(evt, callback);

        public static void CreateRpc<T1, T2, TResult>(string evt, Func<T1, T2, TResult> callback)
            => SubscribeDelegate(evt, callback);

        public static void CreateRpc<T1, T2, T3, TResult>(string evt, Func<T1, T2, T3, TResult> callback)
            => SubscribeDelegate(evt, callback);

        public static void UnsubscribeDelegate(string evt, Delegate callback)
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

        public static void Unsubscribe<T>(string evt, Action<T> callback)
            => UnsubscribeDelegate(evt, callback);

        public static void Unsubscribe<T1, T2>(string evt, Action<T1, T2> callback)
            => UnsubscribeDelegate(evt, callback);

        public static void Unsubscribe<T1, T2, T3>(string evt, Action<T1, T2, T3> callback)
            => UnsubscribeDelegate(evt, callback);

        public static void RemoveRpc<T, TResult>(string evt, Func<T, TResult> callback)
            => UnsubscribeDelegate(evt, callback);

        public static void RemoveRpc<T1, T2, TResult>(string evt, Func<T1, T2, TResult> callback)
            => UnsubscribeDelegate(evt, callback);

        public static void RemoveRpc<T1, T2, T3, TResult>(string evt, Func<T1, T2, T3, TResult> callback)
            => UnsubscribeDelegate(evt, callback);

        private static async void ProcessQueue()
        {
            while (_transport.ReceiveQueue.TryDequeue(out var message))
            {
                await Awaiters.UnityMainThread;

                try
                {
                    var parsed = JsonConvert.DeserializeObject<Message>(message);

                    if (parsed.ResponseId.HasValue)
                    {
                        if (_rpcs == null ||
                           !_rpcs.Remove(parsed.ResponseId.Value, out var callback))
                        {
                            continue;
                        }

                        callback(parsed.Args[0]);
                        continue;
                    }

                    if (_subscriptions == null ||
                       !_subscriptions.TryGetValue(parsed.Event, out var callbacks))
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
    }
}
