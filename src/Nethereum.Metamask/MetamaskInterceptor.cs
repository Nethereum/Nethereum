using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Org.BouncyCastle.Asn1.Ocsp;

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
            if (request.Method == "eth_sendTransaction")
            {
                var transaction = (TransactionInput)request.RawParameters[0];
                transaction.From = _metamaskHostProvider.SelectedAccount;
                request.RawParameters[0] = transaction;
                var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(request.Id, request.Method, GetSelectedAccount(),
                    request.RawParameters)).ConfigureAwait(false);
                return ConvertResponse<T>(response);
            } 
            else if ( request.Method == "eth_signTypedData_v4" )
            {
                var account = GetSelectedAccount();
                var parameters = new object[] { account, request.RawParameters[0] };
                var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(request.Id, request.Method, GetSelectedAccount(),
                   parameters)).ConfigureAwait(false);
                return ConvertResponse<T>(response);
            }
            else if (request.Method == "personal_sign")
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
            if (method == "eth_sendTransaction")
            {
                var transaction = (TransactionInput)paramList[0];
                transaction.From = GetSelectedAccount();
                paramList[0] = transaction;
                var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(route, method, GetSelectedAccount(),
                    paramList)).ConfigureAwait(false);
                return ConvertResponse<T>(response);
            }
            else if (method == "eth_signTypedData_v4" || method == "eth_personalSign")
            {
                var account = GetSelectedAccount();
                var parameters = new object[] { account, paramList[0] };
                var response = await _metamaskInterop.SendAsync(new MetamaskRpcRequestMessage(route, method, GetSelectedAccount(),
                   parameters)).ConfigureAwait(false);
                return ConvertResponse<T>(response);
            }
            else
            {
                var response = await _metamaskInterop.SendAsync(new RpcRequestMessage(route, GetSelectedAccount(), method,
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