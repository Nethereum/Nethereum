using Nethereum.RPC.Chain;
using System.Numerics;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{
    public class ChainFeatureStorage
    {
        public string ChainId { get; set; } = "0";
        public string ChainName { get; set; } = "";
        public bool IsTestnet { get; set; }
        public NativeCurrency? NativeCurrency { get; set; }
        public List<string> HttpRpcs { get; set; } = new();
        public List<string> WsRpcs { get; set; } = new();
        public List<string> Explorers { get; set; } = new();
        public bool SupportEIP155 { get; set; } = true;
        public bool SupportEIP1559 { get; set; } = false;

        public static ChainFeatureStorage FromChainFeature(ChainFeature chain)
        {
            return new ChainFeatureStorage
            {
                ChainId = chain.ChainId.ToString(),
                ChainName = chain.ChainName,
                IsTestnet = chain.IsTestnet,
                NativeCurrency = chain.NativeCurrency,
                HttpRpcs = chain.HttpRpcs ?? new List<string>(),
                WsRpcs = chain.WsRpcs ?? new List<string>(),
                Explorers = chain.Explorers ?? new List<string>(),
                SupportEIP155 = chain.SupportEIP155,
                SupportEIP1559 = chain.SupportEIP1559
            };
        }

        public ChainFeature ToChainFeature()
        {
            return new ChainFeature
            {
                ChainId = BigInteger.Parse(ChainId),
                ChainName = ChainName,
                IsTestnet = IsTestnet,
                NativeCurrency = NativeCurrency,
                HttpRpcs = HttpRpcs,
                WsRpcs = WsRpcs,
                Explorers = Explorers,
                SupportEIP155 = SupportEIP155,
                SupportEIP1559 = SupportEIP1559
            };
        }
    }
}