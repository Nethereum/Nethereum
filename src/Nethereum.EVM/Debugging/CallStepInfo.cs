using Nethereum.Documentation;
using Nethereum.ABI.FunctionEncoding;
using System.Collections.Generic;

namespace Nethereum.EVM.Debugging
{
    [NethereumDocExample(DocSection.EvmSimulator, "debugging", "The decoded call a debugger step is standing in")]
    public class CallStepInfo
    {
        public string TargetAddress { get; set; }
        public string ContractName { get; set; }
        public string CallType { get; set; }
        public string Selector { get; set; }
        public string FunctionName { get; set; }
        public string FunctionSignature { get; set; }
        public List<ParameterOutput> DecodedInputs { get; set; }
        public string RawCalldata { get; set; }
    }
}
