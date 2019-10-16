using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.GSN.DTOs
{
    [Event("RelayAdded")]
    public class RelayAddedEvent : IEventDTO
    {
        [Parameter("address", "relay", 1, true)]
        public string Relay { get; set; }

        [Parameter("address", "owner", 2, true)]
        public string Owner { get; set; }

        [Parameter("uint256", "transactionFee", 3, false)]
        public BigInteger TransactionFee { get; set; }

        [Parameter("uint256", "stake", 4, false)]
        public BigInteger Stake { get; set; }

        [Parameter("uint256", "unstakeDelay", 5, false)]
        public BigInteger UnstakeDelay { get; set; }

        [Parameter("string", "url", 6, false)]
        public string Url { get; set; }
    }
}
