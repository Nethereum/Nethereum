using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.ENS.Deed.ContractDefinition
{
    
    
    public partial class DeedDeployment:DeedDeploymentBase
    {
        public DeedDeployment():base(BYTECODE) { }
        
        public DeedDeployment(string byteCode):base(byteCode) { }
    }

    public class DeedDeploymentBase:ContractDeploymentMessage
    {
        
        public static string BYTECODE = "60806040526040516020806104d5833981016040525160018054600160a060020a03909216600160a060020a0319928316178155600080549092163317909155426003556005805460ff1916909117905534600455610472806100636000396000f3006080604052600436106100a35763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166305b3441081146100a85780630b5ab3d5146100cf57806313af4035146100e65780632b20e397146101075780633fa4f24514610138578063674f220f1461014d5780638da5cb5b14610162578063b0c8097214610177578063bbe4277114610194578063faab9d39146101ac575b600080fd5b3480156100b457600080fd5b506100bd6101cd565b60408051918252519081900360200190f35b3480156100db57600080fd5b506100e46101d3565b005b3480156100f257600080fd5b506100e4600160a060020a0360043516610218565b34801561011357600080fd5b5061011c6102b6565b60408051600160a060020a039092168252519081900360200190f35b34801561014457600080fd5b506100bd6102c5565b34801561015957600080fd5b5061011c6102cb565b34801561016e57600080fd5b5061011c6102da565b34801561018357600080fd5b506100e460043560243515156102e9565b3480156101a057600080fd5b506100e4600435610369565b3480156101b857600080fd5b506100e4600160a060020a0360043516610400565b60035481565b60055460ff16156101e357600080fd5b600154604051600160a060020a0390911690303180156108fc02916000818181858888f19350505050156102165761deadff5b565b600054600160a060020a0316331461022f57600080fd5b600160a060020a038116151561024457600080fd5b600180546002805473ffffffffffffffffffffffffffffffffffffffff19908116600160a060020a03808516919091179092559084169116811790915560408051918252517fa2ea9883a321a3e97b8266c2b078bfeec6d50c711ed71f874a90d500ae2eaf369181900360200190a150565b600054600160a060020a031681565b60045481565b600254600160a060020a031681565b600154600160a060020a031681565b600054600160a060020a0316331461030057600080fd5b60055460ff16151561031157600080fd5b60045482111561032057600080fd5b6004829055600154604051600160a060020a0390911690303184900380156108fc02916000818181858888f193505050508061035a575080155b151561036557600080fd5b5050565b600054600160a060020a0316331461038057600080fd5b60055460ff16151561039157600080fd5b6005805460ff1916905560405161dead906103e83031848203020480156108fc02916000818181858888f1935050505015156103cc57600080fd5b6040517fbb2ce2f51803bba16bc85282b47deeea9a5c6223eabea1077be696b3f265cf1390600090a16103fd6101d3565b50565b600054600160a060020a0316331461041757600080fd5b6000805473ffffffffffffffffffffffffffffffffffffffff1916600160a060020a03929092169190911790555600a165627a7a7230582035d2001b67491c12934109329348e960c6740bd46d7e626189aec1deb86867a70029";
        
        public DeedDeploymentBase():base(BYTECODE) { }
        
        public DeedDeploymentBase(string byteCode):base(byteCode) { }
        
        [Parameter("address", "_owner", 1)]
        public virtual string Owner {get; set;}
    }    
    
    public partial class CreationDateFunction:CreationDateFunctionBase{}

    [Function("creationDate", "uint256")]
    public class CreationDateFunctionBase:FunctionMessage
    {

    }    
    
    public partial class DestroyDeedFunction:DestroyDeedFunctionBase{}

    [Function("destroyDeed")]
    public class DestroyDeedFunctionBase:FunctionMessage
    {

    }    
    
    public partial class SetOwnerFunction:SetOwnerFunctionBase{}

    [Function("setOwner")]
    public class SetOwnerFunctionBase:FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner {get; set;}
    }    
    
    public partial class RegistrarFunction:RegistrarFunctionBase{}

    [Function("registrar", "address")]
    public class RegistrarFunctionBase:FunctionMessage
    {

    }    
    
    public partial class ValueFunction:ValueFunctionBase{}

    [Function("value", "uint256")]
    public class ValueFunctionBase:FunctionMessage
    {

    }    
    
    public partial class PreviousOwnerFunction:PreviousOwnerFunctionBase{}

    [Function("previousOwner", "address")]
    public class PreviousOwnerFunctionBase:FunctionMessage
    {

    }    
    
    public partial class OwnerFunction:OwnerFunctionBase{}

    [Function("owner", "address")]
    public class OwnerFunctionBase:FunctionMessage
    {

    }    
    
    public partial class SetBalanceFunction:SetBalanceFunctionBase{}

    [Function("setBalance")]
    public class SetBalanceFunctionBase:FunctionMessage
    {
        [Parameter("uint256", "newValue", 1)]
        public virtual BigInteger NewValue {get; set;}
        [Parameter("bool", "throwOnFailure", 2)]
        public virtual bool ThrowOnFailure {get; set;}
    }    
    
    public partial class CloseDeedFunction:CloseDeedFunctionBase{}

    [Function("closeDeed")]
    public class CloseDeedFunctionBase:FunctionMessage
    {
        [Parameter("uint256", "refundRatio", 1)]
        public virtual BigInteger RefundRatio {get; set;}
    }    
    
    public partial class SetRegistrarFunction:SetRegistrarFunctionBase{}

    [Function("setRegistrar")]
    public class SetRegistrarFunctionBase:FunctionMessage
    {
        [Parameter("address", "newRegistrar", 1)]
        public virtual string NewRegistrar {get; set;}
    }    
    
    public partial class OwnerChangedEventDTO:OwnerChangedEventDTOBase{}

    [Event("OwnerChanged")]
    public class OwnerChangedEventDTOBase: IEventDTO
    {
        [Parameter("address", "newOwner", 1, false )]
        public virtual string NewOwner {get; set;}
    }    
    
    
    
    public partial class CreationDateOutputDTO:CreationDateOutputDTOBase{}

    [FunctionOutput]
    public class CreationDateOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 {get; set;}
    }    
    
    
    
    
    
    public partial class RegistrarOutputDTO:RegistrarOutputDTOBase{}

    [FunctionOutput]
    public class RegistrarOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 {get; set;}
    }    
    
    public partial class ValueOutputDTO:ValueOutputDTOBase{}

    [FunctionOutput]
    public class ValueOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 {get; set;}
    }    
    
    public partial class PreviousOwnerOutputDTO:PreviousOwnerOutputDTOBase{}

    [FunctionOutput]
    public class PreviousOwnerOutputDTOBase :IFunctionOutputDTO 
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
    
    
    
    
    

}
