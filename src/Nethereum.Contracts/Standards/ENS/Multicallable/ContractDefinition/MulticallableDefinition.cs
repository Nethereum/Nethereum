using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ENS.Multicallable.ContractDefinition
{
    public partial class MulticallFunction : MulticallFunctionBase { }

    [Function("multicall", "bytes[]")]
    public class MulticallFunctionBase : FunctionMessage
    {
        [Parameter("bytes[]", "data", 1)]
        public virtual List<byte[]> Data { get; set; }
    }

    public partial class MulticallOutputDTO : MulticallOutputDTOBase { }

    [FunctionOutput]
    public class MulticallOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes[]", "", 1)]
        public virtual List<byte[]> ReturnValue1 { get; set; }
    }
}
