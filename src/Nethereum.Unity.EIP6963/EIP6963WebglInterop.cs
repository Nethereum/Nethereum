using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Nethereum.Unity.EIP6963
{
    public  class EIP6963WebglInterop
    {
        
        [DllImport("__Internal")]
        public static extern string EIP6963_EnableEthereum(string gameObjectName, string callback, string fallback);

        [DllImport("__Internal")]
        public static extern string EIP6963_GetSelectedAddress();

        [DllImport("__Internal")]
        public static extern void EIP6963_GetChainId(string gameObjectName, string callback, string fallback);
        
        [DllImport("__Internal")]
        public static extern bool EIP6963_IsAvailable();

        [DllImport("__Internal")]
        public static extern void EIP6963_InitEIP6963(); // Unity must call this first on startup

        [DllImport("__Internal")]
        public static extern string EIP6963_GetAvailableWallets();

        [DllImport("__Internal")]
        public static extern void EIP6963_SelectWallet(string walletUuid);

        [DllImport("__Internal")]
        public static extern string EIP6963_GetWalletIcon(string walletUuid);

        
        [DllImport("__Internal")]
        public static extern void EIP6963_EthereumInit(string gameObjectName, string callBackAccountChange, string callBackChainChange);

        [DllImport("__Internal")]
        public static extern void EIP6963_EthereumInitRpcClientCallback(Action<string> callBackAccountChange, Action<string> callBackChainIdChange);

        [DllImport("__Internal")]
        public static extern string EIP6963_Request(string rpcRequestMessage, string gameObjectName, string callback, string fallback);

        [DllImport("__Internal")]
        public static extern void EIP6963_RequestRpcClientCallback(Action<string> rpcResponse, string rpcRequest);
    }

}
