using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ENS.ReverseRegistrar.ContractDefinition
{

    public partial class AddrReverseNodeFunction : AddrReverseNodeFunctionBase { }

    [Function("ADDR_REVERSE_NODE", "bytes32")]
    public class AddrReverseNodeFunctionBase : FunctionMessage
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

    public partial class AddrReverseNodeOutputDto : AddrReverseNodeOutputDtoBase { }

    [FunctionOutput]
    public class AddrReverseNodeOutputDtoBase : IFunctionOutputDTO 
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
