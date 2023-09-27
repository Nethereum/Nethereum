using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace ProtocolContracts.Contracts.OrderValidatorTest.ContractDefinition
{
    [Struct("AssetType")]
    public partial class AssetType
    {
        [Parameter("bytes4", "assetClass", 1)]
        public virtual byte[] AssetClass { get; set; }
        [Parameter("bytes", "data", 2)]
        public virtual byte[] Data { get; set; }
    }
  
}
