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
using System.Collections.Generic;
using System;
using WalletConnectSharp.Sign.Controllers;
using System.Diagnostics;

namespace Nethereum.WalletConnect
{
    public class NethereumWalletConnectService : INethereumWalletConnectService
    {
        public const string MAINNET = "eip155:1";
        
        public static readonly string[] DEFAULT_CHAINS = { MAINNET };

        public ISignClient WalletConnectClient { get; }
        public string SelectedChainId { get => _selectedChainId; 
            protected set => _selectedChainId = value; }
        public string SelectedAccount { get => _selectedAccount; 
            protected set => _selectedAccount = value; }

        public static ConnectOptions GetDefaultConnectOptions()
        {
            return GetDefaultConnectOptions(DEFAULT_CHAINS);
        }

        private string _selectedAccount;
        private string _selectedChainId;
        private ConnectedData _connectedData;

        public NethereumWalletConnectService(ISignClient walletConnectClient)
        {
            WalletConnectClient = walletConnectClient;
            
        }

        public async Task<string> WaitForConnectionApprovalAndGetSelectedAccountAsync()
        {
            if(_connectedData == null)
            {
                throw new InvalidOperationException("Connection not initialised");
            }

           var session = await _connectedData.Approval;
           var address =  session.CurrentAddress(SelectedChainId);
           SelectedAccount = address.Address;
           await SetHostProviderSelectedAccountAsync();
            return address.Address;
        }

        public async Task<string> InitialiseConnectionAndGetQRUriAsync(ConnectOptions connectionOptions = null)
        {
            if (connectionOptions == null)
            {
                connectionOptions = GetDefaultConnectOptions(DEFAULT_CHAINS);
            }
            //ensure we are disconnected
            try
            {
                await WalletConnectClient.Core.Storage.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            SelectedChainId = MAINNET;
            await SetHostProviderSelectedChainAsync();

            _connectedData = await WalletConnectClient.Connect(connectionOptions);
            //await WalletConnectClient.AddressProvider.SetDefaultChainIdAsync(MAINNET); //default to mainnet we can set this to any chain id parameter


            WalletConnectClient.SubscribeToSessionEvent("chainChanged", (sender, session) =>
            {
                if (session.ChainId != null && session.ChainId != "eip155:0" && session.ChainId.StartsWith("eip155"))
                {
                    SelectedChainId = session.ChainId;
                    SetHostProviderSelectedChainAsync().Wait();
                }


            });

            WalletConnectClient.SubscribeToSessionEvent("accountsChanged", (sender, session) =>
            {
                SelectedAccount = SessionStruct.CreateCaip25Address(session.Event.Data[0].ToString()).Address;
                SetHostProviderSelectedAccountAsync().Wait();
            });

            return _connectedData.Uri;
        }

        private async Task SetHostProviderSelectedAccountAsync()
        {
            if (NethereumWalletConnectHostProvider.Current != null)
            {
                await NethereumWalletConnectHostProvider.Current.ChangeSelectedAccountAsync(SelectedAccount);
                }
        }

        private async Task SetHostProviderSelectedChainAsync()
        {
            if (NethereumWalletConnectHostProvider.Current != null)
            {
                await NethereumWalletConnectHostProvider.Current.ChangeSelectedNetworkAsync(GetChainIdFromEip155(SelectedChainId));
            }
        }

        public static string GetEIP155ChainId(long chainId)
        {
            return $"eip155:{chainId}";
        }

        public static long GetChainIdFromEip155(string chainId )
        {
            return long.Parse(chainId.Replace("eip155:", ""));
        }

        public static string[] GetEIP155ChainIds(params long[] chainIds)
        {
            return chainIds.Select(GetEIP155ChainId).ToArray();
        }

        public static ConnectOptions GetDefaultConnectOptions(params long[] optionalEIP155chainIds)
        {
           return GetDefaultConnectOptions(GetEIP155ChainIds(optionalEIP155chainIds));
        }

        public static ConnectOptions GetDefaultConnectOptions(params string[] optionalEIP155chainIds)
        {
            if(optionalEIP155chainIds == null)
            {
                optionalEIP155chainIds = DEFAULT_CHAINS;
            }

            var requiredNamespace = new ProposedNamespace()
            {
                Methods = new[]
                            {
                                ApiMethods.eth_sendTransaction.ToString(),
                            },
                Chains = new[] { MAINNET } ,
                Events = new[]
                            {
                                "chainChanged", "accountsChanged", "connect", "disconnect"
                            }
            };

            var optionalNamespace = new ProposedNamespace()
            {
                Methods = new[]
                           {
                                ApiMethods.eth_sign.ToString(),
                                ApiMethods.personal_sign.ToString(),
                                ApiMethods.eth_signTypedData_v4.ToString(),
                                ApiMethods.wallet_switchEthereumChain.ToString(),
                                ApiMethods.wallet_addEthereumChain.ToString()
                            },
                Chains = optionalEIP155chainIds,
                Events = new[]
                           {
                                "chainChanged", "accountsChanged", "connect", "disconnect"
                            }
            };


            return new ConnectOptions()
            {
                RequiredNamespaces =
                {
                    { "eip155", requiredNamespace }
                },
                OptionalNamespaces =  
                {
                    { "eip155", optionalNamespace }
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
                //await  WalletConnectClient.AddressProvider.SetDefaultChainIdAsync(SelectedChainId);
                return await WalletConnectClient.Request<WCWalletSwitchEthereumChainRequest, string>(connectedSession.Session.Topic, request, SelectedChainId);
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
                //await WalletConnectClient.AddressProvider.SetDefaultChainIdAsync(SelectedChainId);
                return await WalletConnectClient.Request<WCWalletAddEthereumChainRequest, string>(connectedSession.Session.Topic, request, SelectedChainId);
            }
            throw new InvalidWalletConnectSessionException();
        }


        public async Task<string> PersonalSignAsync(string hexUtf8)
        {
            var connectedSession = GetWalletConnectConnectedSession();
            if (connectedSession != null)
            {
                var request = new WCPersonalSign(hexUtf8, connectedSession.Address);
                //await WalletConnectClient.AddressProvider.SetDefaultChainIdAsync(SelectedChainId);
                return await WalletConnectClient.Request<WCPersonalSign, string>(connectedSession.Session.Topic, request, SelectedChainId);
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
                //await WalletConnectClient.AddressProvider.SetDefaultChainIdAsync(SelectedChainId);
                return await WalletConnectClient.Request<WCEthSign, string>(connectedSession.Session.Topic, request, SelectedChainId);
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
                //await WalletConnectClient.AddressProvider.SetDefaultChainIdAsync(SelectedChainId);
                return await WalletConnectClient.Request<WCEthSignTypedData, string>(connectedSession.Session.Topic, request, SelectedChainId);
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
                //await WalletConnectClient.AddressProvider.SetDefaultChainIdAsync(SelectedChainId);
                return await WalletConnectClient.Request<WCEthSignTypedDataV4, string>(connectedSession.Session.Topic, request, SelectedChainId);
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
               // await WalletConnectClient.AddressProvider.SetDefaultChainIdAsync(SelectedChainId);
                return await WalletConnectClient.Request<WCEthSendTransactionRequest, string>(connectedSession.Session.Topic, request,SelectedChainId);
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
               // await WalletConnectClient.AddressProvider.SetDefaultChainIdAsync(SelectedChainId);
                return await WalletConnectClient.Request<WCEthSignTransactionRequest, string>(connectedSession.Session.Topic, request, SelectedChainId);
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
            var currentSession = WalletConnectClient.AddressProvider.DefaultSession;
            
            if (string.IsNullOrWhiteSpace(currentSession.Topic))
                return null;

            //var defaultChainId = WalletConnectClient.AddressProvider.DefaultChainId;

            // var caip25Address = currentSession.CurrentAddress(defaultChainId);

            return new WalletConnectConnectedSession
            {
                Session = currentSession, 
                Address = SelectedAccount, 
                ChainId = SelectedChainId,
            };
        }
    }

}






