using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.ENS.DNSResolver.ContractDefinition
{
    
    
    public partial class DNSResolverDeployment:DNSResolverDeploymentBase
    {
        public DNSResolverDeployment():base(BYTECODE) { }
        
        public DNSResolverDeployment(string byteCode):base(byteCode) { }
    }

    public class DNSResolverDeploymentBase:ContractDeploymentMessage
    {
        
        public static string BYTECODE = "608060405234801561001057600080fd5b5060008054600160a060020a031916331790556103ee806100326000396000f3006080604052600436106100615763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166301ffc9a78114610066578063126a710e146100b157806376196c881461013e5780638da5cb5b1461019e575b600080fd5b34801561007257600080fd5b5061009d7bffffffffffffffffffffffffffffffffffffffffffffffffffffffff19600435166101dc565b604080519115158252519081900360200190f35b3480156100bd57600080fd5b506100c9600435610220565b6040805160208082528351818301528351919283929083019185019080838360005b838110156101035781810151838201526020016100eb565b50505050905090810190601f1680156101305780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561014a57600080fd5b5060408051602060046024803582810135601f810185900485028601850190965285855261019c9583359536956044949193909101919081908401838280828437509497506102c39650505050505050565b005b3480156101aa57600080fd5b506101b361030b565b6040805173ffffffffffffffffffffffffffffffffffffffff9092168252519081900360200190f35b7bffffffffffffffffffffffffffffffffffffffffffffffffffffffff19167f126a710e000000000000000000000000000000000000000000000000000000001490565b60008181526001602081815260409283902080548451600260001995831615610100029590950190911693909304601f810183900483028401830190945283835260609390918301828280156102b75780601f1061028c576101008083540402835291602001916102b7565b820191906000526020600020905b81548152906001019060200180831161029a57829003601f168201915b50505050509050919050565b60005473ffffffffffffffffffffffffffffffffffffffff1633146102e757600080fd5b6000828152600160209081526040909120825161030692840190610327565b505050565b60005473ffffffffffffffffffffffffffffffffffffffff1681565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061036857805160ff1916838001178555610395565b82800160010185558215610395579182015b8281111561039557825182559160200191906001019061037a565b506103a19291506103a5565b5090565b6103bf91905b808211156103a157600081556001016103ab565b905600a165627a7a72305820326595bd17a1515f1427b243cb0201939a60281acb6e1e9984270ccee14235030029";
        
        public DNSResolverDeploymentBase():base(BYTECODE) { }
        
        public DNSResolverDeploymentBase(string byteCode):base(byteCode) { }
        

    }    
    
    public partial class SupportsInterfaceFunction:SupportsInterfaceFunctionBase{}

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase:FunctionMessage
    {
        [Parameter("bytes4", "interfaceID", 1)]
        public virtual byte[] InterfaceID {get; set;}
    }    
    
    public partial class DnsrrFunction:DnsrrFunctionBase{}

    [Function("dnsrr", "bytes")]
    public class DnsrrFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node {get; set;}
    }    
    
    public partial class SetDnsrrFunction:SetDnsrrFunctionBase{}

    [Function("setDnsrr")]
    public class SetDnsrrFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node {get; set;}
        [Parameter("bytes", "data", 2)]
        public virtual byte[] Data {get; set;}
    }    
    
    public partial class OwnerFunction:OwnerFunctionBase{}

    [Function("owner", "address")]
    public class OwnerFunctionBase:FunctionMessage
    {

    }    
    
    public partial class SupportsInterfaceOutputDTO:SupportsInterfaceOutputDTOBase{}

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 {get; set;}
    }    
    
    public partial class DnsrrOutputDTO:DnsrrOutputDTOBase{}

    [FunctionOutput]
    public class DnsrrOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 {get; set;}
    }    
    
    
    
    public partial class OwnerOutputDTO:OwnerOutputDTOBase{}

    [FunctionOutput]
    public class OwnerOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 {get; set;}
    }
}
