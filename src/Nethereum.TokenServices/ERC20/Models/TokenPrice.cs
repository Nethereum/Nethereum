using System;

namespace Nethereum.TokenServices.ERC20.Models
{
    public class TokenPrice
    {
        public string TokenId { get; set; }
        public string ContractAddress { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
