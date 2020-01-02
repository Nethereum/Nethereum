using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.GSN.DTOs
{
    [Event("RelayRemoved")]
    public class RelayRemovedEvent : IEventDTO
    {
        [Parameter("address", "relay", 1, true)]
        public string Relay { get; set; }

        [Parameter("uint256", "unstakeTime", 2, false)]
        public BigInteger UnstakeTime { get; set; }
    }
}
