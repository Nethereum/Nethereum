using System;

namespace Nethereum.TokenServices.ERC20.Models
{
    public class CatalogTokenInfo
    {
        public string Address { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public int Decimals { get; set; }
        public string LogoUri { get; set; }
        public long ChainId { get; set; }
        public string CoinGeckoId { get; set; }

        public DateTime AddedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public string Source { get; set; }

        public CatalogTokenInfo()
        {
            AddedAtUtc = DateTime.UtcNow;
        }

        public static CatalogTokenInfo FromTokenInfo(TokenInfo tokenInfo, string source = "embedded")
        {
            return new CatalogTokenInfo
            {
                Address = tokenInfo.Address,
                Symbol = tokenInfo.Symbol,
                Name = tokenInfo.Name,
                Decimals = tokenInfo.Decimals,
                LogoUri = tokenInfo.LogoUri,
                ChainId = tokenInfo.ChainId,
                CoinGeckoId = tokenInfo.CoinGeckoId,
                AddedAtUtc = DateTime.UtcNow,
                Source = source
            };
        }

        public TokenInfo ToTokenInfo()
        {
            return new TokenInfo
            {
                Address = Address,
                Symbol = Symbol,
                Name = Name,
                Decimals = Decimals,
                LogoUri = LogoUri,
                ChainId = ChainId,
                CoinGeckoId = CoinGeckoId
            };
        }

        public void UpdateFrom(CatalogTokenInfo other)
        {
            if (other == null) return;

            if (!string.IsNullOrEmpty(other.Symbol))
                Symbol = other.Symbol;
            if (!string.IsNullOrEmpty(other.Name))
                Name = other.Name;
            if (other.Decimals > 0)
                Decimals = other.Decimals;
            if (!string.IsNullOrEmpty(other.LogoUri))
                LogoUri = other.LogoUri;
            if (!string.IsNullOrEmpty(other.CoinGeckoId))
                CoinGeckoId = other.CoinGeckoId;

            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
