using System;
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
        private readonly MetamaskHostProvider _metamaskHostProvider;

        public MetamaskInterceptor(IMetamaskInterop metamaskInterop, MetamaskHostProvider metamaskHostProvider)
        {
            _metamaskInterop = metamaskInterop;
            _metamaskHostProvider = metamaskHostProvider;
        }

        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<RpcRequest, string, Task<T>> interceptedSendRequestAsync, RpcRequest request,
            string route = null)
        {
            var newUniqueRequestId = Guid.NewGuid().ToString();
            request.Id = newUniqueRequestId;
            if (request.Method == ApiMethods.eth_sendTransaction.ToString())
            {
                var transaction = (TransactionInput)request.RawParameters[0];
                transaction.From = _metamaskHostProvider.SelectedAccount;
                request.RawParameters[0] = transaction;
                var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(request.Id, request.Method, GetSelectedAccount(),
                    request.RawParameters)).ConfigureAwait(false);
                return ConvertResponse<T>(response);
            } 
            else if (request.Method == ApiMethods.eth_estimateGas.ToString() || request.Method == ApiMethods.eth_call.ToString()) 
            {
                var callinput = (CallInput)request.RawParameters[0];
                if (callinput.From == null)
                {
                    callinput.From ??= _metamaskHostProvider.SelectedAccount;
                    request.RawParameters[0] = callinput;
                }
                var response = await _metamaskInterop.SendAsync(new RpcRequestMessage(request.Id,
                    request.Method,
                    request.RawParameters)).ConfigureAwait(false);
                return ConvertResponse<T>(response);
            } 
            else if ( request.Method == ApiMethods.eth_signTypedData_v4.ToString() )
            {
                var account = GetSelectedAccount();
                var parameters = new object[] { account, request.RawParameters[0] };
                var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(request.Id, request.Method, GetSelectedAccount(),
                   parameters)).ConfigureAwait(false);
                return ConvertResponse<T>(response);
            }
            else if (request.Method == ApiMethods.personal_sign.ToString())
            {
                var account = GetSelectedAccount();
                var parameters = new object[] { request.RawParameters[0], account};
                var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(request.Id, request.Method, GetSelectedAccount(),
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

        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<string, string, object[], Task<T>> interceptedSendRequestAsync, string method,
            string route = null, params object[] paramList)
        {
            var newUniqueRequestId = Guid.NewGuid().ToString();
            route = newUniqueRequestId;
            if (method == ApiMethods.eth_sendTransaction.ToString())
            {
                var transaction = (TransactionInput)paramList[0];
                transaction.From = GetSelectedAccount();
                paramList[0] = transaction;
                var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(route, method, GetSelectedAccount(),
                    paramList)).ConfigureAwait(false);
                return ConvertResponse<T>(response);
            }
            else if (method == ApiMethods.eth_estimateGas.ToString() || method == ApiMethods.eth_call.ToString())
            {
                var callinput = (CallInput)paramList[0];
                if (callinput.From == null)
                {
                    callinput.From ??= _metamaskHostProvider.SelectedAccount;
                    paramList[0] = callinput;
                }
                var response = await _metamaskInterop.SendAsync(new RpcRequestMessage(route, method,
                     paramList)).ConfigureAwait(false);
                return ConvertResponse<T>(response);
            }
            else if (method == ApiMethods.eth_signTypedData_v4.ToString() || method == ApiMethods.personal_sign.ToString())
            {
                var account = GetSelectedAccount();
                var parameters = new object[] { account, paramList[0] };
                var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(route, method, GetSelectedAccount(),
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

        private string GetSelectedAccount()
        {
            return _metamaskHostProvider.SelectedAccount;
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