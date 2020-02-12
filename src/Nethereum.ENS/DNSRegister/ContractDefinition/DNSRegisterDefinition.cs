using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace Nethereum.ENS.DNSRegister.ContractDefinition
{


    public partial class DNSRegisterDeployment : DNSRegisterDeploymentBase
    {
        public DNSRegisterDeployment() : base(BYTECODE) { }
        public DNSRegisterDeployment(string byteCode) : base(byteCode) { }
    }

    public class DNSRegisterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public DNSRegisterDeploymentBase() : base(BYTECODE) { }
        public DNSRegisterDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_dnssec", 1)]
        public virtual string Dnssec { get; set; }
        [Parameter("address", "_ens", 2)]
        public virtual string Ens { get; set; }
    }

    public partial class ClaimFunction : ClaimFunctionBase { }

    [Function("claim")]
    public class ClaimFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "name", 1)]
        public virtual byte[] Name { get; set; }
        [Parameter("bytes", "proof", 2)]
        public virtual byte[] Proof { get; set; }
    }

    public partial class EnsFunction : EnsFunctionBase { }

    [Function("ens", "address")]
    public class EnsFunctionBase : FunctionMessage
    {

    }

    public partial class OracleFunction : OracleFunctionBase { }

    [Function("oracle", "address")]
    public class OracleFunctionBase : FunctionMessage
    {

    }

    public partial class ProveAndClaimFunction : ProveAndClaimFunctionBase { }

    [Function("proveAndClaim")]
    public class ProveAndClaimFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "name", 1)]
        public virtual byte[] Name { get; set; }
        [Parameter("bytes", "input", 2)]
        public virtual byte[] Input { get; set; }
        [Parameter("bytes", "proof", 3)]
        public virtual byte[] Proof { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceID", 1)]
        public virtual byte[] InterfaceID { get; set; }
    }

    public partial class ClaimEventDTO : ClaimEventDTOBase { }

    [Event("Claim")]
    public class ClaimEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true )]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "owner", 2, true )]
        public virtual string Owner { get; set; }
        [Parameter("bytes", "dnsname", 3, false )]
        public virtual byte[] Dnsname { get; set; }
    }



    public partial class EnsOutputDTO : EnsOutputDTOBase { }

    [FunctionOutput]
    public class EnsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class OracleOutputDTO : OracleOutputDTOBase { }

    [FunctionOutput]
    public class OracleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }
}
