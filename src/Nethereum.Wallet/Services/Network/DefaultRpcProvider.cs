using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.Services.Network
{
//    public class DefaultRpcProvider
//    {
//        public virtual RpcConfiguration GetDefaultRpcConfiguration(BigInteger chainId)
//        {
//            return (long)chainId switch
//            {
            

//                _ => new RpcConfiguration
//                {
//                    ChainId = chainId,
//                    HttpRpcs = new List<string>(),
//                    WsRpcs = new List<string>()
//                }
//            };
//        }
//        public ChainFeature ExtendChainWithDefaults(ChainFeature baseChain)
//        {
//            var rpcConfig = GetDefaultRpcConfiguration(baseChain.ChainId);
            
//            var extendedChain = new ChainFeature
//            {
//                ChainId = baseChain.ChainId,
//                ChainName = baseChain.ChainName,
//                NativeCurrency = baseChain.NativeCurrency,
//                SupportEIP155 = baseChain.SupportEIP155,
//                SupportEIP1559 = baseChain.SupportEIP1559,
//                Explorers = baseChain.Explorers,
//                HttpRpcs = new List<string>(rpcConfig.HttpRpcs),
//                WsRpcs = new List<string>(rpcConfig.WsRpcs)
//            };

//            return extendedChain;
//        }
//        public List<ChainFeature> GetAllChainsWithDefaults()
//        {
//            var coreChains = ChainDefaultFeaturesServicesRepository.GetDefaultChainFeatures();
//            return coreChains.Select(ExtendChainWithDefaults).ToList();
//        }
//    }
//    public class RpcConfiguration
//    {
//        public BigInteger ChainId { get; set; }
//        public List<string> HttpRpcs { get; set; } = new();
//        public List<string> WsRpcs { get; set; } = new();
//    }
}