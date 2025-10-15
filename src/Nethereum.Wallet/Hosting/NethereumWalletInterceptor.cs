using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.UI;

namespace Nethereum.Wallet.Hosting
{
    public class NethereumWalletInterceptor: RequestInterceptor
    {
        private readonly RpcHandlerRegistry _handlerRegistry;
        private readonly IWalletContext _walletContext;
        private string? _selectedAccount;
        
        public static List<string> InterceptedMethods { get; } = new List<string>
        {
            "eth_sendTransaction",
            "eth_signTransaction",
            "eth_sign",
            "personal_sign",
            "eth_signTypedData",
            "eth_signTypedData_v3",
            "eth_signTypedData_v4",
            "eth_requestAccounts",
            "wallet_requestPermissions",
            "wallet_switchEthereumChain",
            "wallet_addEthereumChain",
            "eth_accounts"
        };
        
        public NethereumWalletInterceptor(
            RpcHandlerRegistry handlerRegistry,
            IWalletContext walletContext)
        {
            _handlerRegistry = handlerRegistry ?? throw new ArgumentNullException(nameof(handlerRegistry));
            _walletContext = walletContext ?? throw new ArgumentNullException(nameof(walletContext));
        }
        
        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<RpcRequest, string, Task<T>> interceptedSendRequestAsync,
            RpcRequest request,
            string? route = null)
        {
            Console.WriteLine($"Intercepting method: {request.Method}");
            if (request?.Method != null && InterceptedMethods.Contains(request.Method))
            {
                if (_handlerRegistry.TryGetHandler(request.Method, out var handler) && handler != null)
                {
                    try
                    {
                        var rpcRequest = new RpcRequestMessage(request.Id, request.Method, request.RawParameters);
                        var response = await handler.HandleAsync(rpcRequest, _walletContext);
                        var result = response.Result;
                        
                        if (result is T typedResult)
                        {
                            return typedResult;
                        }
                        
                        if (result == null && default(T) == null)
                        {
                            return default(T)!;
                        }
                        
                        return (T)Convert.ChangeType(result, typeof(T));
                    }
                    catch (Exception ex)
                    {
                        throw new RpcClientUnknownException($"Error handling {request.Method}: {ex.Message}", ex);
                    }
                }
            }
            
            return await base.InterceptSendRequestAsync(interceptedSendRequestAsync, request, route);
        }
        
        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<string, string, object[], Task<T>> interceptedSendRequestAsync,
            string method,
            string? route = null,
            params object[] paramList)
        {
            Console.WriteLine($"Intercepting method: {method}");
            if (InterceptedMethods.Contains(method))
            {
                if (_handlerRegistry.TryGetHandler(method, out var handler) && handler != null)
                {
                    var rpcRequest = new RpcRequestMessage(Guid.NewGuid().ToString(), method, paramList);
                    var response = await handler.HandleAsync(rpcRequest, _walletContext);
                    var result = response.Result;
                    
                    if (result is T typedResult)
                    {
                        return typedResult;
                    }
                    
                    if (result == null && default(T) == null)
                    {
                        return default(T)!;
                    }
                    
                    return (T)Convert.ChangeType(result, typeof(T))!;
                }
            }
            
            return await base.InterceptSendRequestAsync(interceptedSendRequestAsync, method, route, paramList);
        }
        
        public void SetSelectedAccount(string? account)
        {
            _selectedAccount = account;
            
            if (!string.IsNullOrEmpty(account))
            {
                _walletContext.SetSelectedAccount(account);
            }
        }
        
        public string? GetSelectedAccount()
        {
            return _selectedAccount;
        }
    }
}