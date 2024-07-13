// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace VirtualMaker.RPC
{
    internal static class UnityRpcLog
    {
        [System.Diagnostics.Conditional("UNITY_RPC_DEBUG")]
        public static void Info(string message)
        {
            Debug.Log($"[UnityRpc] {message}");
        }

        public static void Error(string message)
        {
            Debug.LogError($"[UnityRpc] {message}");
        }

        public static void Exception(Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}