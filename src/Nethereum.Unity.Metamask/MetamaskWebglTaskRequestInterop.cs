using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Unity.Metamask
{
    using AOT;
    using Nethereum.Hex.HexTypes;
#if !DOTNET35
    using Nethereum.Metamask;
    using Nethereum.RPC.AccountSigning;
    using Nethereum.RPC.HostWallet;
    using UnityEngine;

    public class MetamaskWebglTaskRequestInterop : IMetamaskInterop
    {
        public static Dictionary<string, RpcResponseMessage> RequestResponses = new Dictionary<string, RpcResponseMessage>();

        [MonoPInvokeCallback(typeof(Action<string>))]
        public static void MetamaskTaskRequestInteropCallBack(string responseMessage)
        {
            var response = JsonConvert.DeserializeObject<RpcResponseMessage>(responseMessage, DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings());
            RequestResponses.Add((string)response.Id, response);
        }

        [MonoPInvokeCallback(typeof(Action<string>))]
        public static void SelectedAccountChanged(string selectedAccount)
        {
            MetamaskWebglHostProvider.Current.ChangeSelectedAccountAsync(selectedAccount).RunSynchronously();
        }

        [MonoPInvokeCallback(typeof(Action<string>))]
        public static void SelectedNetworkChanged(string chainId)
        {
            MetamaskWebglHostProvider.Current.ChangeSelectedNetworkAsync((long)new HexBigInteger(chainId).Value).RunSynchronously();
        }

        public const int DEFAULT_DELAY_BETWEEN_RESPONSE_CHECK_MILLISECONDS = 1000;
        public int TimeOutMilliseconds { get; private set; }
        public int DelayBetweenResponseCheckMilliseconds { get; private set; }
        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        public bool InitialisedAccountChainEvents { get; set; } = false;

        public MetamaskWebglTaskRequestInterop(JsonSerializerSettings jsonSerializerSettings = null, int timeOutMilliseconds = WaitUntilRequestResponse.DefaultTimeOutMilliSeconds, int delayBetweenResponseCheckMilliseconds = DEFAULT_DELAY_BETWEEN_RESPONSE_CHECK_MILLISECONDS)
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
            var metamaskRequest = new RpcRequestMessage(request.Id, request.Method, request.RawParameters);
            var response = await SendAsync(metamaskRequest);
            var accounts = ConvertResponse<string[]>(response);
            if (!InitialisedAccountChainEvents)
            {
                MetamaskWebglInterop.EthereumInitRpcClientCallback(SelectedAccountChanged, SelectedNetworkChanged);
                InitialisedAccountChainEvents = true;
            }
            if (accounts != null && accounts.Length > 0)
            {
                return accounts[0];
            }
            return null;
        }

        public async Task<bool> CheckMetamaskAvailability()
        {
            return MetamaskWebglInterop.IsMetamaskAvailable();
        }

        public async Task<string> GetSelectedAddress()
        {
            return MetamaskWebglInterop.GetSelectedAddress();
        }

        public async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request)
        {
            var newUniqueRequestId = Guid.NewGuid().ToString();
            try
            {
                request.Id = newUniqueRequestId;
                MetamaskWebglInterop.RequestRpcClientCallback(MetamaskTaskRequestInteropCallBack, JsonConvert.SerializeObject(request, JsonSerializerSettings));
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


            if (MetamaskWebglTaskRequestInterop.RequestResponses.ContainsKey(newUniqueRequestId))
            {
                responseMessage = MetamaskWebglTaskRequestInterop.RequestResponses[newUniqueRequestId];
                RequestResponses.Remove(newUniqueRequestId);
                return responseMessage;
            }
            else
            {
                if (waitUntilRequestResponse.HasTimedOut)
                {
                    throw new Exception($"Metamask Response has timeout after : {TimeOutMilliseconds}");

                }
                throw new Exception("Unexpected error retrieving message");
            }
        }

        public Task<RpcResponseMessage> SendTransactionAsync(Nethereum.Metamask.MetamaskRpcRequestMessage rpcRequestMessage)
        {
            return SendAsync(rpcRequestMessage);
        }

        public async Task<string> SignAsync(string utf8Hex)
        {
            var personalSign = new EthPersonalSign();
            var request = personalSign.BuildRequest(new Hex.HexTypes.HexUTF8String(utf8Hex));
            var account = await GetSelectedAddress();
            var metamaskRequest = new MetamaskRpcRequestMessage(request.Id, request.Method, account, request.RawParameters);
            var response = await SendAsync(metamaskRequest);
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

    }
#endif
}
