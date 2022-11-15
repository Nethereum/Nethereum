using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;

namespace Nethereum.Metamask.Blazor
{

    public class MetamaskBlazorInterop : IMetamaskInterop
    {
        private readonly IJSRuntime _jsRuntime;
        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        public MetamaskBlazorInterop(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
            JsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
        }

        public async ValueTask<string> EnableEthereumAsync()
        {
            return await _jsRuntime.InvokeAsync<string>("NethereumMetamaskInterop.EnableEthereum").ConfigureAwait(false);
        }

        public async ValueTask<bool> CheckMetamaskAvailability()
        {
            return await _jsRuntime.InvokeAsync<bool>("NethereumMetamaskInterop.IsMetamaskAvailable").ConfigureAwait(false);
        }

        public async ValueTask<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage)
        {
            var response = await _jsRuntime.InvokeAsync<string>("NethereumMetamaskInterop.Request", JsonConvert.SerializeObject(rpcRequestMessage, JsonSerializerSettings)).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<RpcResponseMessage>(response);
        }

        public async ValueTask<RpcResponseMessage> SendTransactionAsync(MetamaskRpcRequestMessage rpcRequestMessage)
        {
            var response = await _jsRuntime.InvokeAsync<string>("NethereumMetamaskInterop.Request", JsonConvert.SerializeObject(rpcRequestMessage, JsonSerializerSettings)).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<RpcResponseMessage>(response);
        }

        public async ValueTask<string> SignAsync(string utf8Hex)
        {
            var rpcJsonResponse = await _jsRuntime.InvokeAsync<string>("NethereumMetamaskInterop.Sign", utf8Hex).ConfigureAwait(false);
            return ConvertResponse<string>(rpcJsonResponse);
        }

        public async ValueTask<string> GetSelectedAddress()
        {
            var rpcJsonResponse = await _jsRuntime.InvokeAsync<string>("NethereumMetamaskInterop.GetAddresses").ConfigureAwait(false);
            var accounts = ConvertResponse<string[]>(rpcJsonResponse);
            if (accounts.Length > 0)
                return accounts[0];
            else
                return null;

        }


        [JSInvokable()]
        public static async Task MetamaskAvailableChanged(bool available)
        {
            await MetamaskHostProvider.Current.ChangeMetamaskAvailableAsync(available);
        }

        [JSInvokable()]
        public static async Task SelectedAccountChanged(string selectedAccount)
        {
            await MetamaskHostProvider.Current.ChangeSelectedAccountAsync(selectedAccount);
        }

        [JSInvokable()]
        public static async Task SelectedNetworkChanged(string chainId)
        {
            
            await MetamaskHostProvider.Current.ChangeSelectedNetworkAsync((long)new HexBigInteger(chainId).Value);
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
}