using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace ProtocolContracts.Contracts.OrderValidatorTest.ContractDefinition
{
    [Struct("Order")]
    public partial class Order
    {
        [Parameter("address", "maker", 1)]
        public virtual string Maker { get; set; }
        [Parameter("tuple", "makeAsset", 2, "Asset")]
        public virtual Asset MakeAsset { get; set; }
        [Parameter("address", "taker", 3)]
        public virtual string Taker { get; set; }
        [Parameter("tuple", "takeAsset", 4, "Asset")]
        public virtual Asset TakeAsset { get; set; }
        [Parameter("uint256", "salt", 5)]
        public virtual BigInteger Salt { get; set; }
        [Parameter("uint256", "start", 6)]
        public virtual BigInteger Start { get; set; }
        [Parameter("uint256", "end", 7)]
        public virtual BigInteger End { get; set; }
        [Parameter("bytes4", "dataType", 8)]
        public virtual byte[] DataType { get; set; }
        [Parameter("bytes", "data", 9)]
        public virtual byte[] Data { get; set; }



    }
}
