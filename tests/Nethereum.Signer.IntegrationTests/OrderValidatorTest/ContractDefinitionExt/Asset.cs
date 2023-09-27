using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace ProtocolContracts.Contracts.OrderValidatorTest.ContractDefinition
{
    [Struct("Asset")]
    public partial class Asset 
    {
        [Parameter("tuple", "assetType", 1, "AssetType")]
        public virtual AssetType AssetType { get; set; }

        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
    }

   
}
