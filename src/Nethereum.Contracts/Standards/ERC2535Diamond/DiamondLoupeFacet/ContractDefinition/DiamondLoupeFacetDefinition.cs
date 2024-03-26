using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace Nethereum.Contracts.Standards.ERC2535Diamond.DiamondLoupeFacet.ContractDefinition
{


    public partial class DiamondLoupeFacetDeployment : DiamondLoupeFacetDeploymentBase
    {
        public DiamondLoupeFacetDeployment() : base(BYTECODE) { }
        public DiamondLoupeFacetDeployment(string byteCode) : base(byteCode) { }
    }

    public class DiamondLoupeFacetDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "608060405234801561000f575f80fd5b506106348061001d5f395ff3fe608060405234801561000f575f80fd5b5060043610610055575f3560e01c806301ffc9a71461005957806352ef6b2c146100b95780637a0ed627146100ce578063adfca15e146100e3578063cdffacc614610103575b5f80fd5b6100a4610067366004610443565b6001600160e01b0319165f9081527fc8fcad8db84d3cc18b4c41d551ea0ee66dd599cde068d998e57d5e09332c131f602052604090205460ff1690565b60405190151581526020015b60405180910390f35b6100c1610159565b6040516100b09190610471565b6100d66101c8565b6040516100b09190610501565b6100f66100f136600461057e565b61037c565b6040516100b091906105a4565b610141610111366004610443565b6001600160e01b0319165f9081525f805160206105df83398151915260205260409020546001600160a01b031690565b6040516001600160a01b0390911681526020016100b0565b60605f5f805160206105df833981519152600281018054604080516020808402820181019092528281529394508301828280156101bd57602002820191905f5260205f20905b81546001600160a01b0316815260019091019060200180831161019f575b505050505091505090565b7fc8fcad8db84d3cc18b4c41d551ea0ee66dd599cde068d998e57d5e09332c131e546060905f805160206105df833981519152908067ffffffffffffffff811115610215576102156105b6565b60405190808252806020026020018201604052801561025a57816020015b604080518082019091525f8152606060208201528152602001906001900390816102335790505b5092505f5b81811015610376575f83600201828154811061027d5761027d6105ca565b905f5260205f20015f9054906101000a90046001600160a01b03169050808583815181106102ad576102ad6105ca565b6020908102919091018101516001600160a01b0392831690529082165f9081526001860182526040908190208054825181850281018501909352808352919290919083018282801561034857602002820191905f5260205f20905f905b82829054906101000a900460e01b6001600160e01b0319168152602001906004019060208260030104928301926001038202915080841161030a5790505b505050505085838151811061035f5761035f6105ca565b60209081029190910181015101525060010161025f565b50505090565b6001600160a01b0381165f9081527fc8fcad8db84d3cc18b4c41d551ea0ee66dd599cde068d998e57d5e09332c131d602090815260409182902080548351818402810184019094528084526060935f805160206105df833981519152939092919083018282801561043657602002820191905f5260205f20905f905b82829054906101000a900460e01b6001600160e01b031916815260200190600401906020826003010492830192600103820291508084116103f85790505b5050505050915050919050565b5f60208284031215610453575f80fd5b81356001600160e01b03198116811461046a575f80fd5b9392505050565b602080825282518282018190525f9190848201906040850190845b818110156104b15783516001600160a01b03168352928401929184019160010161048c565b50909695505050505050565b5f815180845260208085019450602084015f5b838110156104f65781516001600160e01b031916875295820195908201906001016104d0565b509495945050505050565b5f60208083018184528085518083526040925060408601915060408160051b8701018488015f5b8381101561057057888303603f19018552815180516001600160a01b0316845287015187840187905261055d878501826104bd565b9588019593505090860190600101610528565b509098975050505050505050565b5f6020828403121561058e575f80fd5b81356001600160a01b038116811461046a575f80fd5b602081525f61046a60208301846104bd565b634e487b7160e01b5f52604160045260245ffd5b634e487b7160e01b5f52603260045260245ffdfec8fcad8db84d3cc18b4c41d551ea0ee66dd599cde068d998e57d5e09332c131ca2646970667358221220917783617bd7c37fdda1410f752d3a73161a06a3584cb55e538da31c149518ee64736f6c63430008170033";
        public DiamondLoupeFacetDeploymentBase() : base(BYTECODE) { }
        public DiamondLoupeFacetDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class FacetAddressFunction : FacetAddressFunctionBase { }

    [Function("facetAddress", "address")]
    public class FacetAddressFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "_functionSelector", 1)]
        public virtual byte[] FunctionSelector { get; set; }
    }

    public partial class FacetAddressesFunction : FacetAddressesFunctionBase { }

    [Function("facetAddresses", "address[]")]
    public class FacetAddressesFunctionBase : FunctionMessage
    {

    }

    public partial class FacetFunctionSelectorsFunction : FacetFunctionSelectorsFunctionBase { }

    [Function("facetFunctionSelectors", "bytes4[]")]
    public class FacetFunctionSelectorsFunctionBase : FunctionMessage
    {
        [Parameter("address", "_facet", 1)]
        public virtual string Facet { get; set; }
    }

    public partial class FacetsFunction : FacetsFunctionBase { }

    [Function("facets", typeof(FacetsOutputDTO))]
    public class FacetsFunctionBase : FunctionMessage
    {

    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "_interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class FacetAddressOutputDTO : FacetAddressOutputDTOBase { }

    [FunctionOutput]
    public class FacetAddressOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "facetAddress_", 1)]
        public virtual string Facetaddress { get; set; }
    }

    public partial class FacetAddressesOutputDTO : FacetAddressesOutputDTOBase { }

    [FunctionOutput]
    public class FacetAddressesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address[]", "facetAddresses_", 1)]
        public virtual List<string> Facetaddresses { get; set; }
    }

    public partial class FacetFunctionSelectorsOutputDTO : FacetFunctionSelectorsOutputDTOBase { }

    [FunctionOutput]
    public class FacetFunctionSelectorsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes4[]", "facetFunctionSelectors_", 1)]
        public virtual List<byte[]> Facetfunctionselectors { get; set; }
    }

    public partial class FacetsOutputDTO : FacetsOutputDTOBase { }

    [FunctionOutput]
    public class FacetsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("tuple[]", "facets_", 1)]
        public virtual List<Facet> Facets { get; set; }
    }

    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }
}
