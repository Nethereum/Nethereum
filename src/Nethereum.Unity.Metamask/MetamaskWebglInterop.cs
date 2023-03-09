using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Nethereum.Unity.Metamask
{

    public  class MetamaskWebglInterop
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
        public static extern void RequestRpcClientCallback(Action<string> rpcResponse, string rpcRequest);

        [DllImport("__Internal")]
        public static extern void EthereumInitRpcClientCallback(Action<string> callBackAccountChange, Action<string> callBackChainIdChange);
    }

}
