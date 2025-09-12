using System.Numerics;

namespace Nethereum.Wallet.UI.Components.SendTransaction.Models
{
    public class GasStrategyDisplay
    {
        public GasStrategy Strategy { get; set; }
        public string? EstimatedTime { get; set; }
        public string? EstimatedCost { get; set; }
        public BigInteger? MaxFee { get; set; }
        public BigInteger? PriorityFee { get; set; }
        public BigInteger? GasPrice { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}