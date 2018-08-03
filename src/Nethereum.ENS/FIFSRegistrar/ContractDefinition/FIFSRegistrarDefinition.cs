using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.ENS.FIFSRegistrar.ContractDefinition
{
    
    
    public partial class FIFSRegistrarDeployment:FIFSRegistrarDeploymentBase
    {
        public FIFSRegistrarDeployment():base(BYTECODE) { }
        
        public FIFSRegistrarDeployment(string byteCode):base(byteCode) { }
    }

    public class FIFSRegistrarDeploymentBase:ContractDeploymentMessage
    {
        
        public static string BYTECODE = "608060405234801561001057600080fd5b5060405160408061029583398101604052805160209091015160008054600160a060020a031916600160a060020a0390931692909217825560015561023a90819061005b90396000f3006080604052600436106100405763ffffffff7c0100000000000000000000000000000000000000000000000000000000600035041663d22057a98114610045575b600080fd5b34801561005157600080fd5b5061007660043573ffffffffffffffffffffffffffffffffffffffff60243516610078565b005b60008054600154604080519182526020808301879052815192839003820183207f02571be30000000000000000000000000000000000000000000000000000000084526004840152905186949373ffffffffffffffffffffffffffffffffffffffff16926302571be392602480830193919282900301818787803b1580156100ff57600080fd5b505af1158015610113573d6000803e3d6000fd5b505050506040513d602081101561012957600080fd5b5051905073ffffffffffffffffffffffffffffffffffffffff81161580610165575073ffffffffffffffffffffffffffffffffffffffff811633145b151561017057600080fd5b60008054600154604080517f06ab592300000000000000000000000000000000000000000000000000000000815260048101929092526024820188905273ffffffffffffffffffffffffffffffffffffffff878116604484015290519216926306ab59239260648084019382900301818387803b1580156101f057600080fd5b505af1158015610204573d6000803e3d6000fd5b50505050505050505600a165627a7a7230582097b6e58210590101cedb393097468ea73c88daf33765459d5c9f272b5ea2fa830029";
        
        public FIFSRegistrarDeploymentBase():base(BYTECODE) { }
        
        public FIFSRegistrarDeploymentBase(string byteCode):base(byteCode) { }
        
        [Parameter("address", "ensAddr", 1)]
        public virtual string EnsAddr {get; set;}
        [Parameter("bytes32", "node", 2)]
        public virtual byte[] Node {get; set;}
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
    

}
