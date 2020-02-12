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

namespace Nethereum.ENS.ENSRegistryWithFallback.ContractDefinition
{

    public partial class ENSRegistryWithFallbackDeployment : ENSRegistryWithFallbackDeploymentBase
    {
        public ENSRegistryWithFallbackDeployment() : base(BYTECODE) { }
        public ENSRegistryWithFallbackDeployment(string byteCode) : base(byteCode) { }
    }

    public class ENSRegistryWithFallbackDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public ENSRegistryWithFallbackDeploymentBase() : base(BYTECODE) { }
        public ENSRegistryWithFallbackDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_old", 1)]
        public virtual string Old { get; set; }
    }

    public partial class IsApprovedForAllFunction : IsApprovedForAllFunctionBase { }

    [Function("isApprovedForAll", "bool")]
    public class IsApprovedForAllFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "operator", 2)]
        public virtual string Operator { get; set; }
    }

    public partial class OldFunction : OldFunctionBase { }

    [Function("old", "address")]
    public class OldFunctionBase : FunctionMessage
    {

    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
    }

    public partial class RecordExistsFunction : RecordExistsFunctionBase { }

    [Function("recordExists", "bool")]
    public class RecordExistsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
    }

    public partial class ResolverFunction : ResolverFunctionBase { }

    [Function("resolver", "address")]
    public class ResolverFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
    }

    public partial class SetApprovalForAllFunction : SetApprovalForAllFunctionBase { }

    [Function("setApprovalForAll")]
    public class SetApprovalForAllFunctionBase : FunctionMessage
    {
        [Parameter("address", "operator", 1)]
        public virtual string Operator { get; set; }
        [Parameter("bool", "approved", 2)]
        public virtual bool Approved { get; set; }
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

    public partial class SetRecordFunction : SetRecordFunctionBase { }

    [Function("setRecord")]
    public class SetRecordFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("address", "resolver", 3)]
        public virtual string Resolver { get; set; }
        [Parameter("uint64", "ttl", 4)]
        public virtual ulong Ttl { get; set; }
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

    public partial class SetSubnodeOwnerFunction : SetSubnodeOwnerFunctionBase { }

    [Function("setSubnodeOwner", "bytes32")]
    public class SetSubnodeOwnerFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes32", "label", 2)]
        public virtual byte[] Label { get; set; }
        [Parameter("address", "owner", 3)]
        public virtual string Owner { get; set; }
    }

    public partial class SetSubnodeRecordFunction : SetSubnodeRecordFunctionBase { }

    [Function("setSubnodeRecord")]
    public class SetSubnodeRecordFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes32", "label", 2)]
        public virtual byte[] Label { get; set; }
        [Parameter("address", "owner", 3)]
        public virtual string Owner { get; set; }
        [Parameter("address", "resolver", 4)]
        public virtual string Resolver { get; set; }
        [Parameter("uint64", "ttl", 5)]
        public virtual ulong Ttl { get; set; }
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

    public partial class ApprovalForAllEventDTO : ApprovalForAllEventDTOBase { }

    [Event("ApprovalForAll")]
    public class ApprovalForAllEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "operator", 2, true )]
        public virtual string Operator { get; set; }
        [Parameter("bool", "approved", 3, false )]
        public virtual bool Approved { get; set; }
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

    public partial class TransferEventDTO : TransferEventDTOBase { }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true )]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "owner", 2, false )]
        public virtual string Owner { get; set; }
    }

    public partial class IsApprovedForAllOutputDTO : IsApprovedForAllOutputDTOBase { }

    [FunctionOutput]
    public class IsApprovedForAllOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class OldOutputDTO : OldOutputDTOBase { }

    [FunctionOutput]
    public class OldOutputDTOBase : IFunctionOutputDTO 
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

    public partial class RecordExistsOutputDTO : RecordExistsOutputDTOBase { }

    [FunctionOutput]
    public class RecordExistsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class ResolverOutputDTO : ResolverOutputDTOBase { }

    [FunctionOutput]
    public class ResolverOutputDTOBase : IFunctionOutputDTO 
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
