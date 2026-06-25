using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.AppChain.Anchoring.ZiskVerifierAdapter.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.ZiskVerifierAdapter.ContractDefinition
{


    public partial class ZiskVerifierAdapterDeployment : ZiskVerifierAdapterDeploymentBase
    {
        public ZiskVerifierAdapterDeployment() : base(BYTECODE) { }
        public ZiskVerifierAdapterDeployment(string byteCode) : base(byteCode) { }
    }

    public class ZiskVerifierAdapterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60a060405234801561000f575f5ffd5b5060405161071638038061071683398101604081905261002e916101ae565b6001600160a01b0383166080526100475f83600461005e565b50610055600182600461005e565b505050506101fe565b6001830191839082156100f2579160200282015f5b838211156100bd57835183826101000a8154816001600160401b0302191690836001600160401b031602179055509260200192600801602081600701049283019260010302610073565b80156100f05782816101000a8154906001600160401b0302191690556008016020816007010492830192600103026100bd565b505b506100fe929150610102565b5090565b5b808211156100fe575f8155600101610103565b80516001600160401b038116811461012c575f5ffd5b919050565b5f82601f830112610140575f5ffd5b604051608081016001600160401b038111828210171561016e57634e487b7160e01b5f52604160045260245ffd5b604052806080840185811115610182575f5ffd5b845b818110156101a35761019581610116565b835260209283019201610184565b509195945050505050565b5f5f5f61012084860312156101c1575f5ffd5b83516001600160a01b03811681146101d7575f5ffd5b92506101e68560208601610131565b91506101f58560a08601610131565b90509250925092565b6080516104fa61021c5f395f8181605301526101ba01526104fa5ff3fe608060405234801561000f575f5ffd5b506004361061004a575f3560e01c80637f64b72f1461004e5780639649daae14610092578063a040b5f3146100b5578063daa6a578146100e1575b5f5ffd5b6100757f000000000000000000000000000000000000000000000000000000000000000081565b6040516001600160a01b0390911681526020015b60405180910390f35b6100a56100a0366004610285565b6100f4565b6040519015158152602001610089565b6100c86100c336600461034d565b610244565b60405167ffffffffffffffff9091168152602001610089565b6100c86100ef36600461034d565b610276565b5f600682101561010557505f61023c565b600484101561011557505f61023c565b5f6101236004828789610364565b61012c9161038b565b60e01c905061013c8160046103c3565b63ffffffff16851015610152575f91505061023c565b365f8760048861016286836103c3565b63ffffffff169261017593929190610364565b9092509050365f89896101898760046103c3565b63ffffffff1690809261019e93929190610364565b604051635f7f35bb60e11b815291935091506001600160a01b037f0000000000000000000000000000000000000000000000000000000000000000169063befe6b76906101fa905f90600190879087908b908b90600401610473565b5f6040518083038186803b158015610210575f5ffd5b505afa925050508015610221575060015b610232575f9550505050505061023c565b6001955050505050505b949350505050565b5f8160048110610252575f80fd5b60049182820401919006600802915054906101000a900467ffffffffffffffff1681565b60018160048110610252575f80fd5b5f5f5f5f60408587031215610298575f5ffd5b843567ffffffffffffffff8111156102ae575f5ffd5b8501601f810187136102be575f5ffd5b803567ffffffffffffffff8111156102d4575f5ffd5b8760208284010111156102e5575f5ffd5b60209182019550935085013567ffffffffffffffff811115610305575f5ffd5b8501601f81018713610315575f5ffd5b803567ffffffffffffffff81111561032b575f5ffd5b8760208260051b840101111561033f575f5ffd5b949793965060200194505050565b5f6020828403121561035d575f5ffd5b5035919050565b5f5f85851115610372575f5ffd5b8386111561037e575f5ffd5b5050820193919092039150565b80356001600160e01b031981169060048410156103bc576001600160e01b0319600485900360031b81901b82161691505b5092915050565b63ffffffff81811683821601908111156103eb57634e487b7160e01b5f52601160045260245ffd5b92915050565b805f5b600460038201101561044557815467ffffffffffffffff8082168652604082811c82166020880152608083811c9092169087015260c09190911c6060860152909301926001909101906004016103f4565b50505050565b81835281816020850137505f828201602090810191909152601f909101601f19169091010190565b61047d81886103f1565b61048a60808201876103f1565b6101406101008201525f6104a36101408301868861044b565b8281036101208401526104b781858761044b565b999850505050505050505056fea2646970667358221220c5da22dfd9a4d2be903e857f78c59d28b6f40670cc5783ecd6db0a6564c59eb464736f6c634300081c0033";
        public ZiskVerifierAdapterDeploymentBase() : base(BYTECODE) { }
        public ZiskVerifierAdapterDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_ziskVerifier", 1)]
        public virtual string ZiskVerifier { get; set; }
        [Parameter("uint64[4]", "_programVK", 2)]
        public virtual List<ulong> ProgramVK { get; set; }
        [Parameter("uint64[4]", "_rootCVadcopFinal", 3)]
        public virtual List<ulong> RootCVadcopFinal { get; set; }
    }

    public partial class ProgramVKFunction : ProgramVKFunctionBase { }

    [Function("programVK", "uint64")]
    public class ProgramVKFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger Index { get; set; }
    }

    public partial class RootCVadcopFinalFunction : RootCVadcopFinalFunctionBase { }

    [Function("rootCVadcopFinal", "uint64")]
    public class RootCVadcopFinalFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger Index { get; set; }
    }

    public partial class VerifyFunction : VerifyFunctionBase { }

    [Function("verify", "bool")]
    public class VerifyFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "proof", 1)]
        public virtual byte[] Proof { get; set; }
        [Parameter("uint256[]", "publicInputs", 2)]
        public virtual List<BigInteger> PublicInputs { get; set; }
    }

    public partial class ZiskVerifierFunction : ZiskVerifierFunctionBase { }

    [Function("ziskVerifier", "address")]
    public class ZiskVerifierFunctionBase : FunctionMessage
    {
    }

    public partial class ProgramVKOutputDTO : ProgramVKOutputDTOBase { }

    [FunctionOutput]
    public class ProgramVKOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class RootCVadcopFinalOutputDTO : RootCVadcopFinalOutputDTOBase { }

    [FunctionOutput]
    public class RootCVadcopFinalOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class VerifyOutputDTO : VerifyOutputDTOBase { }

    [FunctionOutput]
    public class VerifyOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class ZiskVerifierOutputDTO : ZiskVerifierOutputDTOBase { }

    [FunctionOutput]
    public class ZiskVerifierOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }
}
