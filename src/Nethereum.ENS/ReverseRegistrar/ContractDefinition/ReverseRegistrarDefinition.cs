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

namespace Nethereum.ENS.ReverseRegistrar.ContractDefinition
{

    public partial class ReverseRegistrarDeployment : ReverseRegistrarDeploymentBase
    {
        public ReverseRegistrarDeployment() : base(ReverseRegistrarDeploymentBase.BYTECODE) { }
        public ReverseRegistrarDeployment(string byteCode) : base(byteCode) { }
    }

    public class ReverseRegistrarDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public ReverseRegistrarDeploymentBase() : base(BYTECODE) { }
        public ReverseRegistrarDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "ensAddr", 1)]
        public virtual string EnsAddr { get; set; }
        [Parameter("address", "resolverAddr", 2)]
        public virtual string ResolverAddr { get; set; }
    }

    public partial class ADDR_REVERSE_NODEFunction : ADDR_REVERSE_NODEFunctionBase { }

    [Function("ADDR_REVERSE_NODE", "bytes32")]
    public class ADDR_REVERSE_NODEFunctionBase : FunctionMessage
    {

    }

    public partial class ClaimFunction : ClaimFunctionBase { }

    [Function("claim", "bytes32")]
    public class ClaimFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class ClaimWithResolverFunction : ClaimWithResolverFunctionBase { }

    [Function("claimWithResolver", "bytes32")]
    public class ClaimWithResolverFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "resolver", 2)]
        public virtual string Resolver { get; set; }
    }

    public partial class DefaultResolverFunction : DefaultResolverFunctionBase { }

    [Function("defaultResolver", "address")]
    public class DefaultResolverFunctionBase : FunctionMessage
    {

    }

    public partial class EnsFunction : EnsFunctionBase { }

    [Function("ens", "address")]
    public class EnsFunctionBase : FunctionMessage
    {

    }

    public partial class NodeFunction : NodeFunctionBase { }

    [Function("node", "bytes32")]
    public class NodeFunctionBase : FunctionMessage
    {
        [Parameter("address", "addr", 1)]
        public virtual string Addr { get; set; }
    }

    public partial class SetNameFunction : SetNameFunctionBase { }

    [Function("setName", "bytes32")]
    public class SetNameFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
    }

    public partial class ADDR_REVERSE_NODEOutputDTO : ADDR_REVERSE_NODEOutputDTOBase { }

    [FunctionOutput]
    public class ADDR_REVERSE_NODEOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }





    public partial class DefaultResolverOutputDTO : DefaultResolverOutputDTOBase { }

    [FunctionOutput]
    public class DefaultResolverOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class EnsOutputDTO : EnsOutputDTOBase { }

    [FunctionOutput]
    public class EnsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class NodeOutputDTO : NodeOutputDTOBase { }

    [FunctionOutput]
    public class NodeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }


}
