using System;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class CustomToken
    {
        public string ContractAddress { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public int Decimals { get; set; }
        public string LogoURI { get; set; }
        public long ChainId { get; set; }
        public DateTime AddedAt { get; set; }
    }
}
