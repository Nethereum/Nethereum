using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Nethereum.EIP6963WalletInterop;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;

namespace Nethereum.Blazor.EIP6963WalletInterop
{

    public class EIP6963WalletBlazorInterop : IEIP6963WalletInterop
    {
        private readonly IJSRuntime _jsRuntime;
        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        public EIP6963WalletBlazorInterop(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
            JsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
        }

        public async ValueTask<string> EnableEthereumAsync()
        {
            return await _jsRuntime.InvokeAsync<string>("NethereumEIP6963Interop.enableEthereum").ConfigureAwait(false);
        }

        public async ValueTask<bool> CheckAvailabilityAsync()
        {
            return await _jsRuntime.InvokeAsync<bool>("NethereumEIP6963Interop.isAvailable").ConfigureAwait(false);
        }

        public async ValueTask<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage)
        {
            var response = await _jsRuntime.InvokeAsync<string>("NethereumEIP6963Interop.request", JsonConvert.SerializeObject(rpcRequestMessage, JsonSerializerSettings)).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<RpcResponseMessage>(response);
        }

        public async ValueTask<RpcResponseMessage> SendTransactionAsync(EIP6963RpcRequestMessage rpcRequestMessage)
        {
            var response = await _jsRuntime.InvokeAsync<string>("NethereumEIP6963Interop.request", JsonConvert.SerializeObject(rpcRequestMessage, JsonSerializerSettings)).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<RpcResponseMessage>(response);
        }

        public async ValueTask<string> SignAsync(string utf8Hex)
        {
            var rpcJsonResponse = await _jsRuntime.InvokeAsync<string>("NethereumEIP6963Interop.sign", utf8Hex).ConfigureAwait(false);
            return ConvertResponse<string>(rpcJsonResponse);
        }

        public async ValueTask<string> GetSelectedAddress()
        {
            var rpcJsonResponse = await _jsRuntime.InvokeAsync<string>("NethereumEIP6963Interop.getSelectedOrRequestAddress").ConfigureAwait(false);
            if (rpcJsonResponse == null)
                return null;
            var accounts = ConvertResponse<string[]>(rpcJsonResponse);
            if (accounts.Length > 0)
                return accounts[0];
            else
                return null;

        }


        [JSInvokable()]
        public static async Task EIP6963AvailableChanged(bool available)
        {
            await EIP6963WalletHostProvider.Current.ChangeWalletAvailableAsync(available);
        }

        [JSInvokable()]
        public static async Task EIP6963SelectedAccountChanged(string selectedAccount)
        {
            await EIP6963WalletHostProvider.Current.ChangeSelectedAccountAsync(selectedAccount);
        }

        [JSInvokable()]
        public static async Task EIP6963SelectedNetworkChanged(string chainId)
        {
            
            await EIP6963WalletHostProvider.Current.ChangeSelectedNetworkAsync((long)new HexBigInteger(chainId).Value);
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

        public async ValueTask<EIP6963WalletInfo[]> GetAvailableWalletsAsync()
        {
            return await _jsRuntime.InvokeAsync<EIP6963WalletInfo[]>("NethereumEIP6963Interop.getAvailableWallets").ConfigureAwait(false);
        }

        public async ValueTask SelectWalletAsync(string walletId)
        {
            await _jsRuntime.InvokeVoidAsync("NethereumEIP6963Interop.selectWallet", walletId).ConfigureAwait(false);
        }

        public async ValueTask<string> GetWalletIconAsync(string walletId)
        {
            return await _jsRuntime.InvokeAsync<string>("NethereumEIP6963Interop.getWalletIcon", walletId);
        }
    }
}