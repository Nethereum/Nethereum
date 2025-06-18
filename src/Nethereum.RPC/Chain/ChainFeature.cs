using Nethereum.RPC.HostWallet;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Chain
{
    public static class ChainFeatureExtensions
    {
        public static ChainFeature ToChainFeature(this AddEthereumChainParameter param)
        {
            return ChainFeature.FromAddEthereumChainParameter(param);
        }
    }


    public class ChainFeature
    {
        public BigInteger ChainId { get; set; }
        public bool SupportEIP1559 { get; set; } = true;
        public bool SupportEIP155 { get; set; } = true;
        public NativeCurrency NativeCurrency { get; set; } = new NativeCurrency();
        public string ChainName { get; set; }
        public List<string> HttpRpcs { get; set; } = new List<string>();
        public List<string> WsRpcs { get; set; } = new List<string>();
        public List<string> Explorers { get; set; } = new List<string>();

        public AddEthereumChainParameter ToAddEthereumChainParameter()
        {
            return new AddEthereumChainParameter()
            {
                ChainId = new HexBigInteger(ChainId),
                BlockExplorerUrls = Explorers,
                ChainName = ChainName,
                IconUrls = new List<string>(),
                NativeCurrency = NativeCurrency.ToRPCNativeCurrency(),
                RpcUrls = HttpRpcs
            };
        }

        public static ChainFeature FromAddEthereumChainParameter(AddEthereumChainParameter param)
        {
            return new ChainFeature
            {
                ChainId = param.ChainId.Value,
                ChainName = param.ChainName,
                HttpRpcs = param.RpcUrls ?? new List<string>(),
                Explorers = param.BlockExplorerUrls ?? new List<string>(),
                NativeCurrency = new NativeCurrency
                {
                    Name = param.NativeCurrency?.Name,
                    Symbol = param.NativeCurrency?.Symbol,
                    Decimals = (int)(param.NativeCurrency?.Decimals ?? 18)
                }
            };
        }
    }

    public class NativeCurrency
    {
        public string Name { get; set; }
        public string Symbol { get; set; } 
        public int Decimals { get; set; }

        public RPC.HostWallet.NativeCurrency ToRPCNativeCurrency()
        {
            return new RPC.HostWallet.NativeCurrency()
            {
                Name = Name,
                Symbol = Symbol,
                Decimals = (uint)Decimals
            };
        }   
    }

}
