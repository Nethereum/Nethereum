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
                                   HttpRpcs = new List<string>(){ "https://mainnet.infura.io/v3/{INFURA_API_KEY}",
                                                                  "https://api.mycryptoapi.com/eth",
                                                                  "https://ethereum.publicnode.com"},
                                   WsRpcs = new List<string>(){ "wss://mainnet.infura.io/ws/v3/{INFURA_API_KEY}",
                                                                 "wss://ethereum.publicnode.com"},
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
                        HttpRpcs = new List<string>()
                        {
                            "https://mainnet.optimism.io",
                            "https://optimism.publicnode.com",

                        },
                        WsRpcs = new List<string>()
                        {
                            "wss://optimism.publicnode.com",

                        },
                        Explorers = new List<string>()
                        {
                            "https://optimistic.etherscan.io",
                            "https://optimism.blockscout.com",
                        },
                        SupportEIP155 = true

                    }
               };
        }
    }
}
