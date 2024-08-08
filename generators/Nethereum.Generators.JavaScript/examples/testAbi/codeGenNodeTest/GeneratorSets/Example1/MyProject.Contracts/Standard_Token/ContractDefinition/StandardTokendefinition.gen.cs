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

namespace MyProject.Contracts.Standard_Token.ContractDefinition
{


    public partial class StandardTokenDeployment : StandardTokenDeploymentBase
    {
        public StandardTokenDeployment() : base(BYTECODE) { }
        public StandardTokenDeployment(string byteCode) : base(byteCode) { }
    }

    public class StandardTokenDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60806040523480156200001157600080fd5b5060405162000c3d38038062000c3d83398101604081905262000034916200014a565b336000908152602081905260409020849055600284905560036200005984826200026a565b506004805460ff191660ff841617905560056200007782826200026a565b505050505062000336565b634e487b7160e01b600052604160045260246000fd5b600082601f830112620000aa57600080fd5b81516001600160401b0380821115620000c757620000c762000082565b604051601f8301601f19908116603f01168101908282118183101715620000f257620000f262000082565b81604052838152602092508660208588010111156200011057600080fd5b600091505b8382101562000134578582018301518183018401529082019062000115565b6000602085830101528094505050505092915050565b600080600080608085870312156200016157600080fd5b845160208601519094506001600160401b03808211156200018157600080fd5b6200018f8883890162000098565b94506040870151915060ff82168214620001a857600080fd5b606087015191935080821115620001be57600080fd5b50620001cd8782880162000098565b91505092959194509250565b600181811c90821680620001ee57607f821691505b6020821081036200020f57634e487b7160e01b600052602260045260246000fd5b50919050565b601f82111562000265576000816000526020600020601f850160051c81016020861015620002405750805b601f850160051c820191505b8181101562000261578281556001016200024c565b5050505b505050565b81516001600160401b0381111562000286576200028662000082565b6200029e81620002978454620001d9565b8462000215565b602080601f831160018114620002d65760008415620002bd5750858301515b600019600386901b1c1916600185901b17855562000261565b600085815260208120601f198616915b828110156200030757888601518255948401946001909101908401620002e6565b5085821015620003265787850151600019600388901b60f8161c191681555b5050505050600190811b01905550565b6108f780620003466000396000f3fe608060405234801561001057600080fd5b50600436106100c95760003560e01c8063313ce5671161008157806395d89b411161005b57806395d89b41146101d9578063a9059cbb146101e1578063dd62ed3e146101f457600080fd5b8063313ce567146101595780635c6581651461017857806370a08231146101a357600080fd5b806318160ddd116100b257806318160ddd1461010f57806323b872dd1461012657806327e235e31461013957600080fd5b806306fdde03146100ce578063095ea7b3146100ec575b600080fd5b6100d661023a565b6040516100e391906106c8565b60405180910390f35b6100ff6100fa36600461075e565b6102c8565b60405190151581526020016100e3565b61011860025481565b6040519081526020016100e3565b6100ff610134366004610788565b610342565b6101186101473660046107c4565b60006020819052908152604090205481565b6004546101669060ff1681565b60405160ff90911681526020016100e3565b6101186101863660046107e6565b600160209081526000928352604080842090915290825290205481565b6101186101b13660046107c4565b73ffffffffffffffffffffffffffffffffffffffff1660009081526020819052604090205490565b6100d6610574565b6100ff6101ef36600461075e565b610581565b6101186102023660046107e6565b73ffffffffffffffffffffffffffffffffffffffff918216600090815260016020908152604080832093909416825291909152205490565b6003805461024790610819565b80601f016020809104026020016040519081016040528092919081815260200182805461027390610819565b80156102c05780601f10610295576101008083540402835291602001916102c0565b820191906000526020600020905b8154815290600101906020018083116102a357829003601f168201915b505050505081565b33600081815260016020908152604080832073ffffffffffffffffffffffffffffffffffffffff8716808552925280832085905551919290917f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925906103309086815260200190565b60405180910390a35060015b92915050565b73ffffffffffffffffffffffffffffffffffffffff8316600081815260016020908152604080832033845282528083205493835290829052812054909190831180159061038f5750828110155b610420576040517f08c379a000000000000000000000000000000000000000000000000000000000815260206004820152603960248201527f746f6b656e2062616c616e6365206f7220616c6c6f77616e6365206973206c6f60448201527f776572207468616e20616d6f756e74207265717565737465640000000000000060648201526084015b60405180910390fd5b73ffffffffffffffffffffffffffffffffffffffff84166000908152602081905260408120805485929061045590849061089b565b909155505073ffffffffffffffffffffffffffffffffffffffff85166000908152602081905260408120805485929061048f9084906108ae565b90915550507fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff8110156105025773ffffffffffffffffffffffffffffffffffffffff85166000908152600160209081526040808320338452909152812080548592906104fc9084906108ae565b90915550505b8373ffffffffffffffffffffffffffffffffffffffff168573ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef8560405161056191815260200190565b60405180910390a3506001949350505050565b6005805461024790610819565b33600090815260208190526040812054821115610620576040517f08c379a000000000000000000000000000000000000000000000000000000000815260206004820152602f60248201527f746f6b656e2062616c616e6365206973206c6f776572207468616e207468652060448201527f76616c75652072657175657374656400000000000000000000000000000000006064820152608401610417565b336000908152602081905260408120805484929061063f9084906108ae565b909155505073ffffffffffffffffffffffffffffffffffffffff83166000908152602081905260408120805484929061067990849061089b565b909155505060405182815273ffffffffffffffffffffffffffffffffffffffff84169033907fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef90602001610330565b60006020808352835180602085015260005b818110156106f6578581018301518582016040015282016106da565b5060006040828601015260407fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe0601f8301168501019250505092915050565b803573ffffffffffffffffffffffffffffffffffffffff8116811461075957600080fd5b919050565b6000806040838503121561077157600080fd5b61077a83610735565b946020939093013593505050565b60008060006060848603121561079d57600080fd5b6107a684610735565b92506107b460208501610735565b9150604084013590509250925092565b6000602082840312156107d657600080fd5b6107df82610735565b9392505050565b600080604083850312156107f957600080fd5b61080283610735565b915061081060208401610735565b90509250929050565b600181811c9082168061082d57607f821691505b602082108103610866577f4e487b7100000000000000000000000000000000000000000000000000000000600052602260045260246000fd5b50919050565b7f4e487b7100000000000000000000000000000000000000000000000000000000600052601160045260246000fd5b8082018082111561033c5761033c61086c565b8181038181111561033c5761033c61086c56fea26469706673582212204cce0d69c948b2f042c41ebdfc83c4b9c99707cce320d8381ac8c51e1ecc72c664736f6c63430008180033";
        public StandardTokenDeploymentBase() : base(BYTECODE) { }
        public StandardTokenDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("uint256", "_initialAmount", 1)]
        public virtual BigInteger InitialAmount { get; set; }
        [Parameter("string", "_tokenName", 2)]
        public virtual string TokenName { get; set; }
        [Parameter("uint8", "_decimalUnits", 3)]
        public virtual byte DecimalUnits { get; set; }
        [Parameter("string", "_tokenSymbol", 4)]
        public virtual string TokenSymbol { get; set; }
    }

    public partial class AllowanceFunction : AllowanceFunctionBase { }

    [Function("allowance", "uint256")]
    public class AllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "_spender", 2)]
        public virtual string Spender { get; set; }
    }

    public partial class AllowedFunction : AllowedFunctionBase { }

    [Function("allowed", "uint256")]
    public class AllowedFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
    }

    public partial class ApproveFunction : ApproveFunctionBase { }

    [Function("approve", "bool")]
    public class ApproveFunctionBase : FunctionMessage
    {
        [Parameter("address", "_spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "_value", 2)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class BalanceOfFunction : BalanceOfFunctionBase { }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunctionBase : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class BalancesFunction : BalancesFunctionBase { }

    [Function("balances", "uint256")]
    public class BalancesFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class DecimalsFunction : DecimalsFunctionBase { }

    [Function("decimals", "uint8")]
    public class DecimalsFunctionBase : FunctionMessage
    {

    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {

    }

    public partial class SymbolFunction : SymbolFunctionBase { }

    [Function("symbol", "string")]
    public class SymbolFunctionBase : FunctionMessage
    {

    }

    public partial class TotalSupplyFunction : TotalSupplyFunctionBase { }

    [Function("totalSupply", "uint256")]
    public class TotalSupplyFunctionBase : FunctionMessage
    {

    }

    public partial class TransferFunction : TransferFunctionBase { }

    [Function("transfer", "bool")]
    public class TransferFunctionBase : FunctionMessage
    {
        [Parameter("address", "_to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_value", 2)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class TransferFromFunction : TransferFromFunctionBase { }

    [Function("transferFrom", "bool")]
    public class TransferFromFunctionBase : FunctionMessage
    {
        [Parameter("address", "_from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_value", 3)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class ApprovalEventDTO : ApprovalEventDTOBase { }

    [Event("Approval")]
    public class ApprovalEventDTOBase : IEventDTO
    {
        [Parameter("address", "_owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "_spender", 2, true )]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "_value", 3, false )]
        public virtual BigInteger Value { get; set; }
    }

    public partial class TransferEventDTO : TransferEventDTOBase { }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("address", "_from", 1, true )]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 2, true )]
        public virtual string To { get; set; }
        [Parameter("uint256", "_value", 3, false )]
        public virtual BigInteger Value { get; set; }
    }

    public partial class AllowanceOutputDTO : AllowanceOutputDTOBase { }

    [FunctionOutput]
    public class AllowanceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "remaining", 1)]
        public virtual BigInteger Remaining { get; set; }
    }

    public partial class AllowedOutputDTO : AllowedOutputDTOBase { }

    [FunctionOutput]
    public class AllowedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase { }

    [FunctionOutput]
    public class BalanceOfOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "balance", 1)]
        public virtual BigInteger Balance { get; set; }
    }

    public partial class BalancesOutputDTO : BalancesOutputDTOBase { }

    [FunctionOutput]
    public class BalancesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class DecimalsOutputDTO : DecimalsOutputDTOBase { }

    [FunctionOutput]
    public class DecimalsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
    }

    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class SymbolOutputDTO : SymbolOutputDTOBase { }

    [FunctionOutput]
    public class SymbolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class TotalSupplyOutputDTO : TotalSupplyOutputDTOBase { }

    [FunctionOutput]
    public class TotalSupplyOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }




}
