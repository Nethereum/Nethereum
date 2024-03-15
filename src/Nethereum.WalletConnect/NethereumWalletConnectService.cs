using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

using Nethereum.WalletConnect.DTOs;
using Nethereum.WalletConnect.Requests;
using System.Linq;
using System.Threading.Tasks;
using WalletConnectSharp.Sign.Interfaces;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Sign.Models.Engine;
using Nethereum.RPC.HostWallet;

namespace Nethereum.WalletConnect
{
    public class NethereumWalletConnectService : INethereumWalletConnectService
    {
        public const string MAINNET = "eip155:1";
        
        public static readonly string[] DEFAULT_CHAINS = { MAINNET };

        public ISignClient WalletConnectClient { get; }

        public static ConnectOptions GetDefaultConnectOptions()
        {
            return GetDefaultConnectOptions(DEFAULT_CHAINS);
        }

        public NethereumWalletConnectService(ISignClient walletConnectClient)
        {
            WalletConnectClient = walletConnectClient;
           
        }

        public static string GetEIP155ChainId(long chainId)
        {
            return $"eip155:{chainId}";
        }

        public static string[] GetEIP155ChainIds(params long[] chainIds)
        {
            return chainIds.Select(GetEIP155ChainId).ToArray();
        }

        public static ConnectOptions GetDefaultConnectOptions(params long[] eip155chainIds)
        {
           return GetDefaultConnectOptions(GetEIP155ChainIds(eip155chainIds));
        }

        public static ConnectOptions GetDefaultConnectOptions(string[] chainIds)
        {
            return new ConnectOptions()
            {
                RequiredNamespaces = new RequiredNamespaces()
                {
                    {
                        "eip155", new ProposedNamespace()
                        {
                            Methods = new[]
                            {
                                ApiMethods.eth_sendTransaction.ToString(),
                                ApiMethods.eth_sign.ToString(),
                                ApiMethods.personal_sign.ToString(),
                                ApiMethods.eth_signTypedData_v4.ToString(),
                                ApiMethods.wallet_switchEthereumChain.ToString(),
                                ApiMethods.wallet_addEthereumChain.ToString()
                            },
                            Chains = chainIds,
                            Events = new[]
                            {
                                "chainChanged", "accountsChanged"
                            }
                        }
                    }
                }
            };
        }

        public async Task<string> SwitchEthereumChainAsync(SwitchEthereumChainParameter chainId)
        {

            var connectedSession = GetWalletConnectConnectedSession();
            if (connectedSession != null)
            {
                var param = new WCSwitchEthereumChainParameter()
                {
                    ChainId = chainId.ChainId.HexValue
                };

                var request = new WCWalletSwitchEthereumChainRequest(param);

                return await WalletConnectClient.Request<WCWalletSwitchEthereumChainRequest, string>(connectedSession.Session.Topic, request, connectedSession.ChainId);
            }
            throw new InvalidWalletConnectSessionException();
        }

        public async Task<string> AddEthereumChainAsync(AddEthereumChainParameter addEthereumChainParameter)
        {

            var connectedSession = GetWalletConnectConnectedSession();
            if (connectedSession != null)
            {
                var param = ConvertAddEthereumChainParameter(addEthereumChainParameter, connectedSession);

                var request = new WCWalletAddEthereumChainRequest(param);

                return await WalletConnectClient.Request<WCWalletAddEthereumChainRequest, string>(connectedSession.Session.Topic, request, connectedSession.ChainId);
            }
            throw new InvalidWalletConnectSessionException();
        }


        public async Task<string> PersonalSignAsync(string hexUtf8)
        {
            var connectedSession = GetWalletConnectConnectedSession();
            if (connectedSession != null)
            {
                var request = new WCPersonalSign(connectedSession.Address, hexUtf8);

                return await WalletConnectClient.Request<WCPersonalSign, string>(connectedSession.Session.Topic, request, connectedSession.ChainId);
            }
            throw new InvalidWalletConnectSessionException();
        }

        public async Task<string> SignAsync(string hexUtf8)
        {
            var connectedSession = GetWalletConnectConnectedSession();
            if (connectedSession != null)
            {
                var request = new WCEthSign(
                        connectedSession.Address, hexUtf8);

                return await WalletConnectClient.Request<WCEthSign, string>(connectedSession.Session.Topic, request, connectedSession.ChainId);
            }
            throw new InvalidWalletConnectSessionException();
        }

        public async Task<string> SignTypedDataAsync(string hexUtf8)
        {
            var connectedSession = GetWalletConnectConnectedSession();
            if (connectedSession != null)
            {
                var request = new WCEthSignTypedData(
                        connectedSession.Address, hexUtf8);

                return await WalletConnectClient.Request<WCEthSignTypedData, string>(connectedSession.Session.Topic, request, connectedSession.ChainId);
            }
            throw new InvalidWalletConnectSessionException();
        }

        public async Task<string> SignTypedDataV4Async(string hexUtf8)
        {
            var connectedSession = GetWalletConnectConnectedSession();
            if (connectedSession != null)
            {
                var request = new WCEthSignTypedDataV4(
                        connectedSession.Address, hexUtf8);

                return await WalletConnectClient.Request<WCEthSignTypedDataV4, string>(connectedSession.Session.Topic, request, connectedSession.ChainId);
            }
            throw new InvalidWalletConnectSessionException();
        }


        public async Task<string> SendTransactionAsync(TransactionInput transaction)
        {
            var connectedSession = GetWalletConnectConnectedSession();
            if (connectedSession != null)
            {
                var txn = ConvertTransactionInputToWC(transaction, connectedSession);
                var request = new WCEthSendTransactionRequest(txn);
                
                return await WalletConnectClient.Request<WCEthSendTransactionRequest, string>(connectedSession.Session.Topic, request, connectedSession.ChainId);
            }
            throw new InvalidWalletConnectSessionException();

        }

        public async Task<string> SignTransactionAsync(TransactionInput transaction)
        {
            var connectedSession = GetWalletConnectConnectedSession();
            if (connectedSession != null)
            {
                var txn = ConvertTransactionInputToWC(transaction, connectedSession);
                var request = new WCEthSignTransactionRequest(txn);

                return await WalletConnectClient.Request<WCEthSignTransactionRequest, string>(connectedSession.Session.Topic, request, connectedSession.ChainId);
            }
            throw new InvalidWalletConnectSessionException();

        }

        private static WCTransactionInput ConvertTransactionInputToWC(TransactionInput transaction, WalletConnectConnectedSession connectedSession)
        {
            return new WCTransactionInput()
            {
                From = connectedSession.Address,
                To = transaction.To,
                Value = transaction.Value?.HexValue,
                Nonce = transaction.Nonce?.HexValue,
                Gas = transaction.Gas?.HexValue,
                GasPrice = transaction.GasPrice?.HexValue,
                ChainId = transaction.ChainId?.HexValue,
                MaxFeePerGas = transaction.MaxFeePerGas?.HexValue,
                MaxPriorityFeePerGas = transaction.MaxPriorityFeePerGas?.HexValue,
                Type = transaction.Type?.HexValue,
                Data = transaction.Data
            };
        }

        private static WCAddEthereumChainParameter ConvertAddEthereumChainParameter(AddEthereumChainParameter addEthereumChainParameter, WalletConnectConnectedSession connectedSession)
        {
            return new WCAddEthereumChainParameter()
            {
                ChainId = addEthereumChainParameter.ChainId.HexValue,
                BlockExplorerUrls = addEthereumChainParameter.BlockExplorerUrls.ToArray(),
                ChainName = addEthereumChainParameter.ChainName,
                IconUrls = addEthereumChainParameter.IconUrls.ToArray(),
                NativeCurrency = ConvertNativeCurrency(addEthereumChainParameter.NativeCurrency),
                RpcUrls = addEthereumChainParameter.RpcUrls.ToArray()
            };
        }

        private static WCNativeCurrency ConvertNativeCurrency(NativeCurrency nativeCurrency)
        {
            return new WCNativeCurrency()
            {
                Name = nativeCurrency.Name,
                Symbol = nativeCurrency.Symbol,
                Decimals = nativeCurrency.Decimals.ToString()
            };
        }   

        public WalletConnectConnectedSession GetWalletConnectConnectedSession()
        {
            var currentSession = WalletConnectClient.Session.Get(WalletConnectClient.Session.Keys[0]);

            var defaultChain = currentSession.Namespaces.Keys.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(defaultChain))
                return null;

            var defaultNamespace = currentSession.Namespaces[defaultChain];

            if (defaultNamespace.Accounts.Length == 0)
                return null;

            var fullAddress = defaultNamespace.Accounts[0];
            var addressParts = fullAddress.Split(":");

            var address = addressParts[2];
            var chainId = string.Join(':', addressParts.Take(2));

            return new WalletConnectConnectedSession() { Session = currentSession, Address = address, ChainId = chainId };
        }
    }

}






