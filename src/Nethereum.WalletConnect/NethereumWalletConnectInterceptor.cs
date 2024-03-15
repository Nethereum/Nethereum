using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletConnectSharp.Sign;
using Nethereum.RPC.HostWallet;

namespace Nethereum.WalletConnect
{

    public class NethereumWalletConnectInterceptor : RequestInterceptor
    {
        private readonly INethereumWalletConnectService _walletConnectService;
  
        public static List<string> SigningWalletTransactionsMethods { get; protected set; } = new List<string>() {
             ApiMethods.eth_sendTransaction.ToString(),
             ApiMethods.eth_sign.ToString(),
             ApiMethods.personal_sign.ToString(),
             ApiMethods.eth_signTypedData_v4.ToString(),
             ApiMethods.wallet_switchEthereumChain.ToString(),
             ApiMethods.wallet_addEthereumChain.ToString()
        };

        public NethereumWalletConnectInterceptor(INethereumWalletConnectService walletConnectService)
        {
            _walletConnectService = walletConnectService;
        }

        public NethereumWalletConnectInterceptor(WalletConnectSignClient walletConnectSignClient)
        {
            _walletConnectService = new NethereumWalletConnectService(walletConnectSignClient);
        }

        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<RpcRequest, string, Task<T>> interceptedSendRequestAsync, RpcRequest request,
            string route = null)
        {

            if (SigningWalletTransactionsMethods.Contains(request.Method))
            {
                try
                {

                    if (request.Method == ApiMethods.eth_sendTransaction.ToString())
                    {
                        var response = await _walletConnectService.SendTransactionAsync((TransactionInput)request.RawParameters[0]);
                        return (object)response;
                    }
                    else if (request.Method == ApiMethods.eth_sign.ToString())
                    {
                        var response = await _walletConnectService.SignAsync((string)request.RawParameters[0]);
                        return (object)response;
                    }
                    else if (request.Method == ApiMethods.eth_signTypedData_v4.ToString())
                    {
                        var response = await _walletConnectService.SignTypedDataV4Async((string)request.RawParameters[0]);
                        return (object)response;
                    }
                    else if (request.Method == ApiMethods.personal_sign.ToString())
                    {
                        var response = await _walletConnectService.PersonalSignAsync((string)request.RawParameters[0]);
                        return (object)response;
                    }
                    else if(request.Method == ApiMethods.wallet_switchEthereumChain.ToString())
                    {
                        var response = await _walletConnectService.SwitchEthereumChainAsync((SwitchEthereumChainParameter)request.RawParameters[0]);
                        return (object)response;
                    }
                    else if (request.Method == ApiMethods.wallet_addEthereumChain.ToString())
                    {
                        var response = await _walletConnectService.AddEthereumChainAsync((AddEthereumChainParameter)request.RawParameters[0]);
                        return (object)response;
                    }

                    throw new NotImplementedException();
                }
                catch (Exception ex)
                {
                    throw new RpcResponseException(new JsonRpc.Client.RpcError(19999999, ex.Message,
                        null));
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
            if (SigningWalletTransactionsMethods.Contains(method))
            {
                try
                {

                    if (method == ApiMethods.eth_sendTransaction.ToString())
                    {
                        var response = await _walletConnectService.SendTransactionAsync((TransactionInput)paramList[0]);
                        return (object)response;
                    }
                    else if (method == ApiMethods.eth_sign.ToString())
                    {
                        var response = await _walletConnectService.SignAsync((string)paramList[0]);
                        return (object)response;
                    }
                    else if (method == ApiMethods.eth_signTypedData_v4.ToString())
                    {
                        var response = await _walletConnectService.SignTypedDataV4Async((string)paramList[0]);
                        return (object)response;
                    }
                    else if (method == ApiMethods.personal_sign.ToString())
                    {
                        var response = await _walletConnectService.PersonalSignAsync((string)paramList[0]);
                        return (object)response;
                    }
                    else if (method == ApiMethods.wallet_switchEthereumChain.ToString())
                    {
                        var response = await _walletConnectService.SwitchEthereumChainAsync((SwitchEthereumChainParameter)paramList[0]);
                        return (object)response;
                    }
                    else if (method == ApiMethods.wallet_addEthereumChain.ToString())
                    {
                        var response = await _walletConnectService.AddEthereumChainAsync((AddEthereumChainParameter)paramList[0]);
                        return (object)response;
                    }

                    throw new NotImplementedException();
                }
                catch (Exception ex)
                {
                    throw new RpcResponseException(new JsonRpc.Client.RpcError(19999999, ex.Message,
                        null));
                }
            }
            else
            {
                return await base.InterceptSendRequestAsync(interceptedSendRequestAsync, method, route, paramList).ConfigureAwait(false);
            }

        }

    }
}

