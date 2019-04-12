using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Structs.StructSample.ContractDefinition
{
    public partial class LineItem : LineItemBase { }

    public class LineItemBase 
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint256", "productId", 2)]
        public virtual BigInteger ProductId { get; set; }
        [Parameter("uint256", "quantity", 3)]
        public virtual BigInteger Quantity { get; set; }
    }
}
