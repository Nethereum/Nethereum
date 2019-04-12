using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Structs.StructSample.ContractDefinition
{
    public partial class PurchaseOrder : PurchaseOrderBase { }

    public class PurchaseOrderBase 
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("tuple[]", "lineItem", 2)]
        public virtual List<LineItem> LineItem { get; set; }
        [Parameter("uint256", "customerId", 3)]
        public virtual BigInteger CustomerId { get; set; }
    }
}
