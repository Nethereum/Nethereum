using System;
using System.Numerics;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class AccountToken
    {
        public string ContractAddress { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public int Decimals { get; set; }
        public string LogoURI { get; set; }
        public long ChainId { get; set; }
        public BigInteger Balance { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool IsCustom { get; set; }
        public bool IsHidden { get; set; }
        public bool IsNative { get; set; }
        public string CoinGeckoId { get; set; }
        public decimal? Price { get; set; }
        public decimal? Value { get; set; }
        public string PriceCurrency { get; set; }
        public DateTime? PriceLastUpdated { get; set; }
    }
}
