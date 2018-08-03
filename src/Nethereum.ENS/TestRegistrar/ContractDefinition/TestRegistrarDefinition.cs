using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.ENS.TestRegistrar.ContractDefinition
{
    
    
    public partial class TestRegistrarDeployment:TestRegistrarDeploymentBase
    {
        public TestRegistrarDeployment():base(BYTECODE) { }
        
        public TestRegistrarDeployment(string byteCode):base(byteCode) { }
    }

    public class TestRegistrarDeploymentBase:ContractDeploymentMessage
    {
        
        public static string BYTECODE = "608060405234801561001057600080fd5b5060405160408061029c83398101604052805160209091015160008054600160a060020a031916600160a060020a0390931692909217825560015561024190819061005b90396000f3006080604052600436106100615763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416633f15457f8114610066578063af9f26e4146100a4578063d22057a9146100ce578063faff50a814610101575b600080fd5b34801561007257600080fd5b5061007b610116565b6040805173ffffffffffffffffffffffffffffffffffffffff9092168252519081900360200190f35b3480156100b057600080fd5b506100bc600435610132565b60408051918252519081900360200190f35b3480156100da57600080fd5b506100ff60043573ffffffffffffffffffffffffffffffffffffffff60243516610144565b005b34801561010d57600080fd5b506100bc61020f565b60005473ffffffffffffffffffffffffffffffffffffffff1681565b60026020526000908152604090205481565b600082815260026020526040902054421161015e57600080fd5b600082815260026020526040808220426224ea00019055815460015482517f06ab592300000000000000000000000000000000000000000000000000000000815260048101919091526024810186905273ffffffffffffffffffffffffffffffffffffffff8581166044830152925192909116926306ab59239260648084019382900301818387803b1580156101f357600080fd5b505af1158015610207573d6000803e3d6000fd5b505050505050565b600154815600a165627a7a72305820d768ee3f427f37eb9032bbd4dc0bcd0a7c7d99c06b713664daff91ad5cfe9f3b0029";
        
        public TestRegistrarDeploymentBase():base(BYTECODE) { }
        
        public TestRegistrarDeploymentBase(string byteCode):base(byteCode) { }
        
        [Parameter("address", "ensAddr", 1)]
        public virtual string EnsAddr {get; set;}
        [Parameter("bytes32", "node", 2)]
        public virtual byte[] Node {get; set;}
    }    
    
    public partial class EnsFunction:EnsFunctionBase{}

    [Function("ens", "address")]
    public class EnsFunctionBase:FunctionMessage
    {

    }    
    
    public partial class ExpiryTimesFunction:ExpiryTimesFunctionBase{}

    [Function("expiryTimes", "uint256")]
    public class ExpiryTimesFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 {get; set;}
    }    
    
    public partial class RegisterFunction:RegisterFunctionBase{}

    [Function("register")]
    public class RegisterFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "subnode", 1)]
        public virtual byte[] Subnode {get; set;}
        [Parameter("address", "owner", 2)]
        public virtual string Owner {get; set;}
    }    
    
    public partial class RootNodeFunction:RootNodeFunctionBase{}

    [Function("rootNode", "bytes32")]
    public class RootNodeFunctionBase:FunctionMessage
    {

    }    
    
    public partial class EnsOutputDTO:EnsOutputDTOBase{}

    [FunctionOutput]
    public class EnsOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 {get; set;}
    }    
    
    public partial class ExpiryTimesOutputDTO:ExpiryTimesOutputDTOBase{}

    [FunctionOutput]
    public class ExpiryTimesOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 {get; set;}
    }    
    
    
    
    public partial class RootNodeOutputDTO:RootNodeOutputDTOBase{}

    [FunctionOutput]
    public class RootNodeOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 {get; set;}
    }
}
