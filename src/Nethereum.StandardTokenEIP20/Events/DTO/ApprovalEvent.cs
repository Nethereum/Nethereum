using System;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.Extensions;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.StandardTokenEIP20.Events.DTO
{
    [Event("Approval")]
    [Obsolete("Please use ApprovalEventDTO instead")]
    public partial class Approval : IEventDTO
    {
        [Parameter("address", "owner", 1, true)]
        public string AddressOwner { get; set; }

        [Parameter("address", "spender", 2, true)]
        public string AddressSpender { get; set; }

        [Parameter("uint", "value", 3)]
        public BigInteger Value { get; set; }
    }
}