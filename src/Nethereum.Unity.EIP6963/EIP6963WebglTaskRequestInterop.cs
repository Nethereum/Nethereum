using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Unity.EIP6963
{
    using AOT;
    using Nethereum.Hex.HexTypes;
#if !DOTNET35
    using Nethereum.EIP6963WalletInterop;
    using Nethereum.RPC.AccountSigning;
    using Nethereum.RPC.HostWallet;
    using UnityEngine;
    using Nethereum.Unity.Rpc;

    public class EIP6963WebglTaskRequestInterop : IEIP6963WalletInterop
    {
        public static Dictionary<string, RpcResponseMessage> RequestResponses = new Dictionary<string, RpcResponseMessage>();

        [MonoPInvokeCallback(typeof(Action<string>))]
        public static void EIP6963_TaskRequestInteropCallBack(string responseMessage)
        {
            var response = JsonConvert.DeserializeObject<RpcResponseMessage>(responseMessage, DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings());
            RequestResponses.Add((string)response.Id, response);
        }

        [MonoPInvokeCallback(typeof(Action<string>))]
        public static void EIP6963_SelectedAccountChanged(string selectedAccount)
        {
            EIP6963WebglHostProvider.Current.ChangeSelectedAccountAsync(selectedAccount).RunSynchronously();
        }

        [MonoPInvokeCallback(typeof(Action<string>))]
        public static void EIP6963_SelectedNetworkChanged(string chainId)
        {
            EIP6963WebglHostProvider.Current.ChangeSelectedNetworkAsync((long)new HexBigInteger(chainId).Value).RunSynchronously();
        }

        public const int DEFAULT_DELAY_BETWEEN_RESPONSE_CHECK_MILLISECONDS = 1000;
        public int TimeOutMilliseconds { get; private set; }
        public int DelayBetweenResponseCheckMilliseconds { get; private set; }
        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        public bool InitialisedAccountChainEvents { get; set; } = false;

        public EIP6963WebglTaskRequestInterop(JsonSerializerSettings jsonSerializerSettings = null, int timeOutMilliseconds = WaitUntilRequestResponse.DefaultTimeOutMilliSeconds, int delayBetweenResponseCheckMilliseconds = DEFAULT_DELAY_BETWEEN_RESPONSE_CHECK_MILLISECONDS)
        {
            TimeOutMilliseconds = timeOutMilliseconds;

            if (jsonSerializerSettings == null)
            {
                JsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            }
            else
            {
                JsonSerializerSettings = jsonSerializerSettings;

            }
            DelayBetweenResponseCheckMilliseconds = delayBetweenResponseCheckMilliseconds;
        }

      

        public async Task<string> EnableEthereumAsync()
        {
            var requestAccounts = new EthRequestAccounts();
            var request = requestAccounts.BuildRequest();
            var rpcRequest = new RpcRequestMessage(request.Id, request.Method, request.RawParameters);
            var response = await SendAsync(rpcRequest);
            var accounts = ConvertResponse<string[]>(response);
            if (!InitialisedAccountChainEvents)
            {
                EIP6963WebglInterop.EIP6963_EthereumInitRpcClientCallback(EIP6963_SelectedAccountChanged, EIP6963_SelectedNetworkChanged);
                InitialisedAccountChainEvents = true;
            }
            if (accounts != null && accounts.Length > 0)
            {
                return accounts[0];
            }
            return null;
        }

        public async Task<bool> CheckAvailabilityAsync()
        {
            return EIP6963WebglInterop.EIP6963_IsAvailable();
        }

        public async Task<string> GetSelectedAddress()
        {
            return EIP6963WebglInterop.EIP6963_GetSelectedAddress();
        }

        public async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request)
        {
            var newUniqueRequestId = Guid.NewGuid().ToString();
            try
            {
                request.Id = newUniqueRequestId;
                EIP6963WebglInterop.EIP6963_RequestRpcClientCallback(EIP6963_TaskRequestInteropCallBack, JsonConvert.SerializeObject(request, JsonSerializerSettings));
            }
            catch (Exception ex)
            {
                throw ex;
            }

            var waitUntilRequestResponse = new WaitUntilRequestResponse(newUniqueRequestId, RequestResponses, TimeOutMilliseconds);
            while (!waitUntilRequestResponse.HasCompletedResponse())
            {
                await Task.Delay(DelayBetweenResponseCheckMilliseconds);
            }
            RpcResponseMessage responseMessage = null;


            if (EIP6963WebglTaskRequestInterop.RequestResponses.ContainsKey(newUniqueRequestId))
            {
                responseMessage = EIP6963WebglTaskRequestInterop.RequestResponses[newUniqueRequestId];
                RequestResponses.Remove(newUniqueRequestId);
                return responseMessage;
            }
            else
            {
                if (waitUntilRequestResponse.HasTimedOut)
                {
                    throw new Exception($"Response has timeout after : {TimeOutMilliseconds}");

                }
                throw new Exception("Unexpected error retrieving message");
            }
        }

        public Task<RpcResponseMessage> SendTransactionAsync(Nethereum.EIP6963WalletInterop.EIP6963RpcRequestMessage rpcRequestMessage)
        {
            return SendAsync(rpcRequestMessage);
        }

        public async Task<string> SignAsync(string utf8Hex)
        {
            var personalSign = new EthPersonalSign();
            var request = personalSign.BuildRequest(new Hex.HexTypes.HexUTF8String(utf8Hex));
            var account = await GetSelectedAddress();
            var eiprequest = new EIP6963RpcRequestMessage(request.Id, request.Method, account, request.RawParameters);
            var response = await SendAsync(eiprequest);
            return ConvertResponse<string>(response);
        }

        private T ConvertResponse<T>(string jsonResponseMessage)
        {
            var responseMessage = JsonConvert.DeserializeObject<RpcResponseMessage>(jsonResponseMessage, JsonSerializerSettings);
            return ConvertResponse<T>(responseMessage);
        }

        private void HandleRpcError(RpcResponseMessage response)
        {
            if (response.HasError)
                throw new RpcResponseException(new JsonRpc.Client.RpcError(response.Error.Code, response.Error.Message,
                    response.Error.Data));
        }

        private T ConvertResponse<T>(RpcResponseMessage response,
           string route = null)
        {
            HandleRpcError(response);
            try
            {
                return response.GetResult<T>();
            }
            catch (FormatException formatException)
            {
                throw new RpcResponseFormatException("Invalid format found in RPC response", formatException);
            }
        }

        public async Task<EIP6963WalletInfo[]> GetAvailableWalletsAsync()
        {
            var wallets = EIP6963WebglInterop.EIP6963_GetAvailableWallets();
            return JsonConvert.DeserializeObject<EIP6963WalletInfo[]>(wallets, JsonSerializerSettings);
        }

        public async Task SelectWalletAsync(string walletId)
        {
            EIP6963WebglInterop.EIP6963_SelectWallet(walletId);
        }

        public async Task<string> GetWalletIconAsync(string walletId)
        {
            return EIP6963WebglInterop.EIP6963_GetWalletIcon(walletId);
        }

        public void InitEIP6963()
        {
            EIP6963WebglInterop.EIP6963_InitEIP6963();
        }
    }
#endif
}
