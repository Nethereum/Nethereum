using System.Numerics;
using Nethereum.Util;

namespace Nethereum.TokenServices.ERC20.Models
{
    public class TokenBalance
    {
        public TokenInfo Token { get; set; }
        public BigInteger Balance { get; set; }
        public bool IsNative { get; set; }

        public decimal BalanceDecimal =>
            UnitConversion.Convert.FromWei(Balance, Token?.Decimals ?? 18);

        public decimal? Price { get; set; }
        public decimal? Value => Price.HasValue ? BalanceDecimal * Price.Value : null;
        public string PriceCurrency { get; set; }
    }
}
