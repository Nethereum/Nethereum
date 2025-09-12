using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.HostWallet;

namespace Nethereum.RPC.Chain
{
    public static class ChainDefaultFeaturesServicesRepository
    {
        public static ChainFeature GetDefaultChainFeature(Signer.Chain chain)
        {
            return GetDefaultChainFeatures().Find(c => c.ChainId == (BigInteger)(int)chain);
        }

        public static List<ChainFeature> GetDefaultChainFeatures()
        {
            return new List<ChainFeature>()
            {
                new ChainFeature(){
                                   ChainId = new BigInteger((int)Signer.Chain.MainNet),
                                   ChainName="Ethereum Mainnet",
                                   NativeCurrency = new NativeCurrency(){Name = "Ether", Symbol = "ETH", Decimals = 18},
                                   HttpRpcs = new List<string>(), // Core library is provider-agnostic
                                   WsRpcs = new List<string>(),   // RPCs configured by wallet implementations
                                   Explorers = new List<string>(){"https://etherscan.io/", "https://eth.blockscout.com"},
                                   SupportEIP155 = true,
                                   SupportEIP1559 = true},

               new ChainFeature()
                    {
                        ChainId = new BigInteger((int)Signer.Chain.Optimism),
                        ChainName = "OP Mainnet",
                        NativeCurrency = new NativeCurrency()
                        {
                            Name = "Ether",
                            Symbol = "ETH",
                            Decimals = 18
                        },
                        HttpRpcs = new List<string>(), // Core library is provider-agnostic
                        WsRpcs = new List<string>(),   // RPCs configured by wallet implementations
                        Explorers = new List<string>()
                        {
                            "https://optimistic.etherscan.io",
                            "https://optimism.blockscout.com",
                        },
                        SupportEIP155 = true,
                        SupportEIP1559 = true
                    },

                // Keep major chains with metadata but no RPC endpoints
                // RPCs will be configured by wallet implementations and ChainList integration
                
                // Polygon (Matic) - Chain ID 137
                new ChainFeature()
                {
                    ChainId = new BigInteger((int)Signer.Chain.Polygon),
                    ChainName = "Polygon Mainnet",
                    NativeCurrency = new NativeCurrency()
                    {
                        Name = "MATIC",
                        Symbol = "MATIC",
                        Decimals = 18
                    },
                    HttpRpcs = new List<string>(), // Core library is provider-agnostic
                    WsRpcs = new List<string>(),   // RPCs configured by wallet implementations
                    Explorers = new List<string>()
                    {
                        "https://polygonscan.com",
                        "https://polygon.blockscout.com"
                    },
                    SupportEIP155 = true,
                    SupportEIP1559 = true
                },

                // BNB Smart Chain - Chain ID 56
                new ChainFeature()
                {
                    ChainId = new BigInteger((int)Signer.Chain.Binance),
                    ChainName = "BNB Smart Chain",
                    NativeCurrency = new NativeCurrency()
                    {
                        Name = "BNB",
                        Symbol = "BNB",
                        Decimals = 18
                    },
                    HttpRpcs = new List<string>(), // Core library is provider-agnostic
                    WsRpcs = new List<string>(),   // RPCs configured by wallet implementations
                    Explorers = new List<string>()
                    {
                        "https://bscscan.com"
                    },
                    SupportEIP155 = true,
                    SupportEIP1559 = false
                },

                // Goerli Testnet - Chain ID 5
                new ChainFeature()
                {
                    ChainId = new BigInteger((int)Signer.Chain.Goerli),
                    ChainName = "Goerli Testnet",
                    NativeCurrency = new NativeCurrency()
                    {
                        Name = "Goerli Ether",
                        Symbol = "GoETH",
                        Decimals = 18
                    },
                    HttpRpcs = new List<string>(), // Core library is provider-agnostic
                    WsRpcs = new List<string>(),   // RPCs configured by wallet implementations
                    Explorers = new List<string>()
                    {
                        "https://goerli.etherscan.io"
                    },
                    SupportEIP155 = true,
                    SupportEIP1559 = true
                },

                // Sepolia Testnet - Chain ID 11155111
                new ChainFeature()
                {
                    ChainId = new BigInteger((int)Signer.Chain.Sepolia),
                    ChainName = "Sepolia Testnet",
                    NativeCurrency = new NativeCurrency()
                    {
                        Name = "Sepolia Ether",
                        Symbol = "SepoliaETH",
                        Decimals = 18
                    },
                    HttpRpcs = new List<string>(), // Core library is provider-agnostic
                    WsRpcs = new List<string>(),   // RPCs configured by wallet implementations
                    Explorers = new List<string>()
                    {
                        "https://sepolia.etherscan.io"
                    },
                    SupportEIP155 = true,
                    SupportEIP1559 = true
                }
               };
        }
    }
}
