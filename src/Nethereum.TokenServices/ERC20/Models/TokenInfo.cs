namespace Nethereum.TokenServices.ERC20.Models
{
    public class TokenInfo
    {
        public string Address { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public int Decimals { get; set; }
        public string LogoUri { get; set; }
        public long ChainId { get; set; }
        public string CoinGeckoId { get; set; }
    }
}
