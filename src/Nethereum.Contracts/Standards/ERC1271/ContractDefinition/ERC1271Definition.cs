using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ERC1271.ContractDefinition
{
    public partial class IsValidSignatureFunction : IsValidSignatureFunctionBase { }

    [Function("isValidSignature", "bytes4")]
    public class IsValidSignatureFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "_hash", 1)]
        public virtual byte[] Hash { get; set; }
        [Parameter("bytes", "_signature", 2)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class IsValidSignatureOutputDTO : IsValidSignatureOutputDTOBase { }

    [FunctionOutput]
    public class IsValidSignatureOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes4", "magicValue", 1)]
        public virtual byte[] MagicValue { get; set; }
    }
}
