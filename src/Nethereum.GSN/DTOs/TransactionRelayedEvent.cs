using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.GSN.Models;
using System.Numerics;

namespace Nethereum.GSN.DTOs
{
    [Event("TransactionRelayed")]
    public class TransactionRelayedEvent : IEventDTO
    {
        [Parameter("address", "relay", 1, true)]
        public string Relay { get; set; }

        [Parameter("address", "from", 2, true)]
        public string From { get; set; }

        [Parameter("address", "to", 3, true)]
        public string To { get; set; }

        [Parameter("bytes4", "selector", 4, false)]
        public byte[] Selector { get; set; }

        [Parameter("uint8", "status", 5, false)]
        public RelayCallStatus Status { get; set; }

        [Parameter("uint256", "charge", 6, false)]
        public BigInteger Charge { get; set; }
    }
}
