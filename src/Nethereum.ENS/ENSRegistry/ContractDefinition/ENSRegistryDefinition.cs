using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.ENS.ENSRegistry.ContractDefinition
{
    
    
    public partial class ENSRegistryDeployment:ENSRegistryDeploymentBase
    {
        public ENSRegistryDeployment():base(BYTECODE) { }
        
        public ENSRegistryDeployment(string byteCode):base(byteCode) { }
    }

    public class ENSRegistryDeploymentBase:ContractDeploymentMessage
    {
        
        public static string BYTECODE = "608060405234801561001057600080fd5b5060008080526020527fad3228b676f7d3cd4284a5443f17f1962b36e491b30a40b2405849e597ba5fb58054600160a060020a03191633179055610500806100596000396000f3006080604052600436106100825763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416630178b8bf811461008757806302571be3146100bb57806306ab5923146100d357806314ab9038146100fc57806316a25cbd146101215780631896f70a146101565780635b0fc9c31461017a575b600080fd5b34801561009357600080fd5b5061009f60043561019e565b60408051600160a060020a039092168252519081900360200190f35b3480156100c757600080fd5b5061009f6004356101bc565b3480156100df57600080fd5b506100fa600435602435600160a060020a03604435166101d7565b005b34801561010857600080fd5b506100fa60043567ffffffffffffffff60243516610291565b34801561012d57600080fd5b5061013960043561035a565b6040805167ffffffffffffffff9092168252519081900360200190f35b34801561016257600080fd5b506100fa600435600160a060020a0360243516610391565b34801561018657600080fd5b506100fa600435600160a060020a0360243516610434565b600090815260208190526040902060010154600160a060020a031690565b600090815260208190526040902054600160a060020a031690565b6000838152602081905260408120548490600160a060020a031633146101fc57600080fd5b60408051868152602080820187905282519182900383018220600160a060020a03871683529251929450869288927fce0457fe73731f824cc272376169235128c118b49d344817417c6d108d155e8292908290030190a3506000908152602081905260409020805473ffffffffffffffffffffffffffffffffffffffff1916600160a060020a03929092169190911790555050565b6000828152602081905260409020548290600160a060020a031633146102b657600080fd5b6040805167ffffffffffffffff84168152905184917f1d4f9bbfc9cab89d66e1a1562f2233ccbf1308cb4f63de2ead5787adddb8fa68919081900360200190a250600091825260208290526040909120600101805467ffffffffffffffff90921674010000000000000000000000000000000000000000027fffffffff0000000000000000ffffffffffffffffffffffffffffffffffffffff909216919091179055565b60009081526020819052604090206001015474010000000000000000000000000000000000000000900467ffffffffffffffff1690565b6000828152602081905260409020548290600160a060020a031633146103b657600080fd5b60408051600160a060020a0384168152905184917f335721b01866dc23fbee8b6b2c7b1e14d6f05c28cd35a2c934239f94095602a0919081900360200190a250600091825260208290526040909120600101805473ffffffffffffffffffffffffffffffffffffffff1916600160a060020a03909216919091179055565b6000828152602081905260409020548290600160a060020a0316331461045957600080fd5b60408051600160a060020a0384168152905184917fd4735d920b0f87494915f556dd9b54c8f309026070caea5c737245152564d266919081900360200190a250600091825260208290526040909120805473ffffffffffffffffffffffffffffffffffffffff1916600160a060020a039092169190911790555600a165627a7a723058206acf003d83f708a2526aad40fa380164c9ea027964140541287f36c78191ccdf0029";
        
        public ENSRegistryDeploymentBase():base(BYTECODE) { }
        
        public ENSRegistryDeploymentBase(string byteCode):base(byteCode) { }
        

    }    
    
    public partial class ResolverFunction:ResolverFunctionBase{}

    [Function("resolver", "address")]
    public class ResolverFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node {get; set;}
    }    
    
    public partial class OwnerFunction:OwnerFunctionBase{}

    [Function("owner", "address")]
    public class OwnerFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node {get; set;}
    }    
    
    public partial class SetSubnodeOwnerFunction:SetSubnodeOwnerFunctionBase{}

    [Function("setSubnodeOwner")]
    public class SetSubnodeOwnerFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node {get; set;}
        [Parameter("bytes32", "label", 2)]
        public virtual byte[] Label {get; set;}
        [Parameter("address", "owner", 3)]
        public virtual string Owner {get; set;}
    }    
    
    public partial class SetTTLFunction:SetTTLFunctionBase{}

    [Function("setTTL")]
    public class SetTTLFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node {get; set;}
        [Parameter("uint64", "ttl", 2)]
        public virtual ulong Ttl {get; set;}
    }    
    
    public partial class TtlFunction:TtlFunctionBase{}

    [Function("ttl", "uint64")]
    public class TtlFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node {get; set;}
    }    
    
    public partial class SetResolverFunction:SetResolverFunctionBase{}

    [Function("setResolver")]
    public class SetResolverFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node {get; set;}
        [Parameter("address", "resolver", 2)]
        public virtual string Resolver {get; set;}
    }    
    
    public partial class SetOwnerFunction:SetOwnerFunctionBase{}

    [Function("setOwner")]
    public class SetOwnerFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node {get; set;}
        [Parameter("address", "owner", 2)]
        public virtual string Owner {get; set;}
    }    
    
    public partial class NewOwnerEventDTO:NewOwnerEventDTOBase{}

    [Event("NewOwner")]
    public class NewOwnerEventDTOBase: IEventDTO
    {
        [Parameter("bytes32", "node", 1, true )]
        public virtual byte[] Node {get; set;}
        [Parameter("bytes32", "label", 2, true )]
        public virtual byte[] Label {get; set;}
        [Parameter("address", "owner", 3, false )]
        public virtual string Owner {get; set;}
    }    
    
    public partial class TransferEventDTO:TransferEventDTOBase{}

    [Event("Transfer")]
    public class TransferEventDTOBase: IEventDTO
    {
        [Parameter("bytes32", "node", 1, true )]
        public virtual byte[] Node {get; set;}
        [Parameter("address", "owner", 2, false )]
        public virtual string Owner {get; set;}
    }    
    
    public partial class NewResolverEventDTO:NewResolverEventDTOBase{}

    [Event("NewResolver")]
    public class NewResolverEventDTOBase: IEventDTO
    {
        [Parameter("bytes32", "node", 1, true )]
        public virtual byte[] Node {get; set;}
        [Parameter("address", "resolver", 2, false )]
        public virtual string Resolver {get; set;}
    }    
    
    public partial class NewTTLEventDTO:NewTTLEventDTOBase{}

    [Event("NewTTL")]
    public class NewTTLEventDTOBase: IEventDTO
    {
        [Parameter("bytes32", "node", 1, true )]
        public virtual byte[] Node {get; set;}
        [Parameter("uint64", "ttl", 2, false )]
        public virtual ulong Ttl {get; set;}
    }    
    
    public partial class ResolverOutputDTO:ResolverOutputDTOBase{}

    [FunctionOutput]
    public class ResolverOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 {get; set;}
    }    
    
    public partial class OwnerOutputDTO:OwnerOutputDTOBase{}

    [FunctionOutput]
    public class OwnerOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 {get; set;}
    }    
    
    
    
    
    
    public partial class TtlOutputDTO:TtlOutputDTOBase{}

    [FunctionOutput]
    public class TtlOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 {get; set;}
    }    
    
    
    

}
