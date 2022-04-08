using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.GnosisSafe.GnosisSafe.ContractDefinition
{
    public partial class InsideOwnerInfo : InsideOwnerInfoBase { }

    public class InsideOwnerInfoBase 
    {
        [Parameter("int256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
    }
}
