using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ENS.ENSRegistry.ContractDefinition
{

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
