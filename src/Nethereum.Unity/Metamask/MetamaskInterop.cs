using AOT;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Unity.RpcModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Nethereum.Unity.Metamask
{
    public  class MetamaskInterop
    {
        [DllImport("__Internal")]
        public static extern string EnableEthereum(string gameObjectName, string callback, string fallback);

        [DllImport("__Internal")]
        public static extern void EthereumInit(string gameObjectName, string callBackAccountChange, string callBackChainChange);

        [DllImport("__Internal")]
        public static extern void GetChainId(string gameObjectName, string callback, string fallback);

        [DllImport("__Internal")]
        public static extern bool IsMetamaskAvailable();

        [DllImport("__Internal")]
        public static extern string GetSelectedAddress();

        [DllImport("__Internal")]
        public static extern string Request(string rpcRequestMessage, string gameObjectName, string callback, string fallback);

        [DllImport("__Internal")]
        public static extern string RequestRpcClientCallback(Action<string> rpcResponse, string rpcRequest);

    }
}
