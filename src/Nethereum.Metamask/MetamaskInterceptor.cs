using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Metamask
{
    public class MetamaskInterceptor : RequestInterceptor
    {
        private readonly IMetamaskInterop _metamaskInterop;
        private readonly bool _useOnlySigningWalletTransactionMethods;

        public static List<string> SigningWalletTransactionsMethods { get; protected set; } = new List<string>() {
            "eth_sendTransaction",
            "eth_signTransaction",
            "eth_sign",
            "personal_sign",
            "eth_signTypedData",
            "eth_signTypedData_v3",
            "eth_signTypedData_v4",
            "wallet_watchAsset",
            "wallet_addEthereumChain",
            "wallet_switchEthereumChain"
        };

        public string SelectedAccount { get; set; }

        public MetamaskInterceptor(IMetamaskInterop metamaskInterop, bool useOnlySigningWalletTransactionMethods = false)
        {
            _metamaskInterop = metamaskInterop;
            _useOnlySigningWalletTransactionMethods = useOnlySigningWalletTransactionMethods;
        }

        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<RpcRequest, string, Task<T>> interceptedSendRequestAsync, RpcRequest request,
            string route = null)
        {

            if (_useOnlySigningWalletTransactionMethods == false || (_useOnlySigningWalletTransactionMethods && SigningWalletTransactionsMethods.Contains(request.Method)))
            {
                var newUniqueRequestId = Guid.NewGuid().ToString();
                request.Id = newUniqueRequestId;
                if (request.Method == ApiMethods.eth_sendTransaction.ToString())
                {
                    var transaction = (TransactionInput)request.RawParameters[0];
                    transaction.From = SelectedAccount;
                    request.RawParameters[0] = transaction;
                    var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(request.Id, request.Method, SelectedAccount,
                        request.RawParameters)).ConfigureAwait(false);
                    return ConvertResponse<T>(response);
                }
                else if (request.Method == ApiMethods.eth_estimateGas.ToString() || request.Method == ApiMethods.eth_call.ToString())
                {
                    var callinput = (CallInput)request.RawParameters[0];
                    if (callinput.From == null)
                    {
                        callinput.From ??= SelectedAccount;
                        request.RawParameters[0] = callinput;
                    }
                    var response = await _metamaskInterop.SendAsync(new RpcRequestMessage(request.Id,
                        request.Method,
                        request.RawParameters)).ConfigureAwait(false);
                    return ConvertResponse<T>(response);
                }
                else if (request.Method == ApiMethods.eth_signTypedData_v4.ToString())
                {
                    var account = SelectedAccount;
                    var parameters = new object[] { account, request.RawParameters[0] };
                    var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(request.Id, request.Method, SelectedAccount,
                       parameters)).ConfigureAwait(false);
                    return ConvertResponse<T>(response);
                }
                else if (request.Method == ApiMethods.personal_sign.ToString())
                {
                    var account = SelectedAccount;
                    var parameters = new object[] { request.RawParameters[0], account };
                    var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(request.Id, request.Method, SelectedAccount,
                       parameters)).ConfigureAwait(false);
                    return ConvertResponse<T>(response);
                }
                else
                {
                    var response = await _metamaskInterop.SendAsync(new RpcRequestMessage(request.Id,
                        request.Method,
                        request.RawParameters)).ConfigureAwait(false);
                    return ConvertResponse<T>(response);
                }
            }
            else
            {
                return await base.InterceptSendRequestAsync(interceptedSendRequestAsync, request, route)
                .ConfigureAwait(false);
            }

        }

        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<string, string, object[], Task<T>> interceptedSendRequestAsync, string method,
            string route = null, params object[] paramList)
        {
            if (_useOnlySigningWalletTransactionMethods == false || (_useOnlySigningWalletTransactionMethods && SigningWalletTransactionsMethods.Contains(method)))
            {
                var newUniqueRequestId = Guid.NewGuid().ToString();
                route = newUniqueRequestId;
                if (method == ApiMethods.eth_sendTransaction.ToString())
                {
                    var transaction = (TransactionInput)paramList[0];
                    transaction.From = SelectedAccount;
                    paramList[0] = transaction;
                    var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(route, method, SelectedAccount,
                        paramList)).ConfigureAwait(false);
                    return ConvertResponse<T>(response);
                }
                else if (method == ApiMethods.eth_estimateGas.ToString() || method == ApiMethods.eth_call.ToString())
                {
                    var callinput = (CallInput)paramList[0];
                    if (callinput.From == null)
                    {
                        callinput.From ??= SelectedAccount;
                        paramList[0] = callinput;
                    }
                    var response = await _metamaskInterop.SendAsync(new RpcRequestMessage(route, method,
                         paramList)).ConfigureAwait(false);
                    return ConvertResponse<T>(response);
                }
                else if (method == ApiMethods.eth_signTypedData_v4.ToString() || method == ApiMethods.personal_sign.ToString())
                {
                    var account = SelectedAccount;
                    var parameters = new object[] { account, paramList[0] };
                    var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(route, method, SelectedAccount,
                       parameters)).ConfigureAwait(false);
                    return ConvertResponse<T>(response);
                }
                else
                {
                    var response = await _metamaskInterop.SendAsync(new RpcRequestMessage(route, method,
                        paramList)).ConfigureAwait(false);
                    return ConvertResponse<T>(response);
                }
            }
            else
            {
                return await base.InterceptSendRequestAsync(interceptedSendRequestAsync, method, route, paramList).ConfigureAwait(false);
            }
          
        }

        protected void HandleRpcError(RpcResponseMessage response)
        {
            if (response.HasError)
                throw new RpcResponseException(new JsonRpc.Client.RpcError(response.Error.Code, response.Error.Message,
                    response.Error.Data));
        }

        private  T ConvertResponse<T>(RpcResponseMessage response,
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