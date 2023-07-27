using System;

namespace Nethereum.DataServices.Etherscan
{
    public enum EtherscanChain
    {
        Mainnet,
        Binance,
        Optimism
    }


   
    public enum EtherscanResultSort
    {
        Ascending,
        Descending
    }

    public static class EtherscanExtensions
    {
        public static string ConvertToRequestFormattedString(this EtherscanResultSort value)
        {
            switch (value)
            {
                case EtherscanResultSort.Ascending:
                    return "asc";
                 case EtherscanResultSort.Descending:
                   return "desc";
            }

            throw new NotImplementedException();
        }
    }
}
