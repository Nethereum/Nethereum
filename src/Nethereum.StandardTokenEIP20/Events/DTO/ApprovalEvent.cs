using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.StandardTokenEIP20.Events.DTO
{
    public class Approval
    {
        [Parameter("address", "owner", 1, true)]
        public string AddressOwner { get; set; }

        [Parameter("address", "spender", 2, true)]
        public string AddressSpender { get; set; }

        [Parameter("uint", "value", 3)]
        public BigInteger Value { get; set; }
    }
}