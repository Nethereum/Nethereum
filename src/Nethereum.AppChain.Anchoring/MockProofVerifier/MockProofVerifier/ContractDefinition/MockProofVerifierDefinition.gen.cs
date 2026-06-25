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
using Nethereum.AppChain.Anchoring.MockProofVerifier.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.MockProofVerifier.ContractDefinition
{


    public partial class MockProofVerifierDeployment : MockProofVerifierDeploymentBase
    {
        public MockProofVerifierDeployment() : base(BYTECODE) { }
        public MockProofVerifierDeployment(string byteCode) : base(byteCode) { }
    }

    public class MockProofVerifierDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x6080604052348015600e575f5ffd5b506102198061001c5f395ff3fe608060405234801561000f575f5ffd5b5060043610610029575f3560e01c80639649daae1461002d575b5f5ffd5b61004061003b366004610107565b610054565b604051901515815260200160405180910390f35b5f610100841461006557505f6100ff565b600682101561007557505f6100ff565b843580610085575f9150506100ff565b83835f818110610097576100976101cf565b905060200201355f036100ad575f9150506100ff565b600b8390036100f957838360048181106100c9576100c96101cf565b90506020020135848460038181106100e3576100e36101cf565b9050602002013511156100f9575f9150506100ff565b60019150505b949350505050565b5f5f5f5f6040858703121561011a575f5ffd5b843567ffffffffffffffff811115610130575f5ffd5b8501601f81018713610140575f5ffd5b803567ffffffffffffffff811115610156575f5ffd5b876020828401011115610167575f5ffd5b60209182019550935085013567ffffffffffffffff811115610187575f5ffd5b8501601f81018713610197575f5ffd5b803567ffffffffffffffff8111156101ad575f5ffd5b8760208260051b84010111156101c1575f5ffd5b949793965060200194505050565b634e487b7160e01b5f52603260045260245ffdfea2646970667358221220df275641feb2d9506410acb12887f21058fa09edcdf401d38fb99bcdc3994eca64736f6c634300081c0033";
        public MockProofVerifierDeploymentBase() : base(BYTECODE) { }
        public MockProofVerifierDeploymentBase(string byteCode) : base(byteCode) { }

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

    public partial class VerifyOutputDTO : VerifyOutputDTOBase { }

    [FunctionOutput]
    public class VerifyOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }
}
