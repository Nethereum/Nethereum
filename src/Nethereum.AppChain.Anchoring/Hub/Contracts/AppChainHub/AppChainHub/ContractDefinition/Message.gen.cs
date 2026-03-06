using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition
{
    public partial class Message : MessageBase { }

    public class MessageBase 
    {
        [Parameter("uint64", "sourceChainId", 1)]
        public virtual ulong SourceChainId { get; set; }
        [Parameter("address", "sender", 2)]
        public virtual string Sender { get; set; }
        [Parameter("uint64", "targetChainId", 3)]
        public virtual ulong TargetChainId { get; set; }
        [Parameter("address", "target", 4)]
        public virtual string Target { get; set; }
        [Parameter("bytes", "data", 5)]
        public virtual byte[] Data { get; set; }
        [Parameter("uint256", "timestamp", 6)]
        public virtual BigInteger Timestamp { get; set; }
    }
}
