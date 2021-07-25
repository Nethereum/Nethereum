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

namespace Nethereum.ENS.ENSRegistry.ContractDefinition
{


    public partial class ENSRegistryDeployment : ENSRegistryDeploymentBase
    {
        public ENSRegistryDeployment() : base(BYTECODE) { }
        public ENSRegistryDeployment(string byteCode) : base(byteCode) { }
    }

    public class ENSRegistryDeploymentBase : ContractDeploymentMessage
    {
#if !BYTECODELITE
        public static string BYTECODE = "3360206000015561021a806100146000396000f3630178b8bf60e060020a600035041415610020576004355460405260206040f35b6302571be360e060020a600035041415610044576020600435015460405260206040f35b6316a25cbd60e060020a600035041415610068576040600435015460405260206040f35b635b0fc9c360e060020a6000350414156100b557602060043501543314151561008f576002565b6024356020600435015560243560405260043560198061020160003960002060206040a2005b6306ab592360e060020a6000350414156101135760206004350154331415156100dc576002565b6044356020600435600052602435602052604060002001556044356040526024356004356021806101e060003960002060206040a3005b631896f70a60e060020a60003504141561015d57602060043501543314151561013a576002565b60243560043555602435604052600435601c806101c460003960002060206040a2005b6314ab903860e060020a6000350414156101aa576020600435015433141515610184576002565b602435604060043501556024356040526004356016806101ae60003960002060206040a2005b6002564e657754544c28627974657333322c75696e743634294e65775265736f6c76657228627974657333322c61646472657373294e65774f776e657228627974657333322c627974657333322c61646472657373295472616e7366657228627974657333322c6164647265737329";
#else
        public static string BYTECODE = "";
#endif
        public ENSRegistryDeploymentBase() : base(BYTECODE) { }
        public ENSRegistryDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class ResolverFunction : ResolverFunctionBase { }

    [Function("resolver", "address")]
    public class ResolverFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
    }

    public partial class SetSubnodeOwnerFunction : SetSubnodeOwnerFunctionBase { }

    [Function("setSubnodeOwner")]
    public class SetSubnodeOwnerFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes32", "label", 2)]
        public virtual byte[] Label { get; set; }
        [Parameter("address", "owner", 3)]
        public virtual string Owner { get; set; }
    }

    public partial class SetTTLFunction : SetTTLFunctionBase { }

    [Function("setTTL")]
    public class SetTTLFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("uint64", "ttl", 2)]
        public virtual ulong Ttl { get; set; }
    }

    public partial class TtlFunction : TtlFunctionBase { }

    [Function("ttl", "uint64")]
    public class TtlFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
    }

    public partial class SetResolverFunction : SetResolverFunctionBase { }

    [Function("setResolver")]
    public class SetResolverFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "resolver", 2)]
        public virtual string Resolver { get; set; }
    }

    public partial class SetOwnerFunction : SetOwnerFunctionBase { }

    [Function("setOwner")]
    public class SetOwnerFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
    }

    public partial class TransferEventDTO : TransferEventDTOBase { }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true )]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "owner", 2, false )]
        public virtual string Owner { get; set; }
    }

    public partial class NewOwnerEventDTO : NewOwnerEventDTOBase { }

    [Event("NewOwner")]
    public class NewOwnerEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true )]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes32", "label", 2, true )]
        public virtual byte[] Label { get; set; }
        [Parameter("address", "owner", 3, false )]
        public virtual string Owner { get; set; }
    }

    public partial class NewResolverEventDTO : NewResolverEventDTOBase { }

    [Event("NewResolver")]
    public class NewResolverEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true )]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "resolver", 2, false )]
        public virtual string Resolver { get; set; }
    }

    public partial class NewTTLEventDTO : NewTTLEventDTOBase { }

    [Event("NewTTL")]
    public class NewTTLEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true )]
        public virtual byte[] Node { get; set; }
        [Parameter("uint64", "ttl", 2, false )]
        public virtual ulong Ttl { get; set; }
    }

    public partial class ResolverOutputDTO : ResolverOutputDTOBase { }

    [FunctionOutput]
    public class ResolverOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }





    public partial class TtlOutputDTO : TtlOutputDTOBase { }

    [FunctionOutput]
    public class TtlOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }




}
