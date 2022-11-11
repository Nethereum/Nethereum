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

namespace Nethereum.Optimism.L2StandardERC20.ContractDefinition
{


    public partial class L2StandardERC20Deployment : L2StandardERC20DeploymentBase
    {
        public L2StandardERC20Deployment() : base(BYTECODE) { }
        public L2StandardERC20Deployment(string byteCode) : base(byteCode) { }
    }

    public class L2StandardERC20DeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "60806040523480156200001157600080fd5b50604051620010b3380380620010b383398101604081905262000034916200022f565b8151829082906200004d9060039060208501906200009f565b508051620000639060049060208401906200009f565b5050600580546001600160a01b039586166001600160a01b031991821617909155600680549690951695169490941790925550620002fc915050565b828054620000ad90620002bf565b90600052602060002090601f016020900481019282620000d157600085556200011c565b82601f10620000ec57805160ff19168380011785556200011c565b828001600101855582156200011c579182015b828111156200011c578251825591602001919060010190620000ff565b506200012a9291506200012e565b5090565b5b808211156200012a57600081556001016200012f565b80516001600160a01b03811681146200015d57600080fd5b919050565b634e487b7160e01b600052604160045260246000fd5b600082601f8301126200018a57600080fd5b81516001600160401b0380821115620001a757620001a762000162565b604051601f8301601f19908116603f01168101908282118183101715620001d257620001d262000162565b81604052838152602092508683858801011115620001ef57600080fd5b600091505b83821015620002135785820183015181830184015290820190620001f4565b83821115620002255760008385830101525b9695505050505050565b600080600080608085870312156200024657600080fd5b620002518562000145565b9350620002616020860162000145565b60408601519093506001600160401b03808211156200027f57600080fd5b6200028d8883890162000178565b93506060870151915080821115620002a457600080fd5b50620002b38782880162000178565b91505092959194509250565b600181811c90821680620002d457607f821691505b60208210811415620002f657634e487b7160e01b600052602260045260246000fd5b50919050565b610da7806200030c6000396000f3fe608060405234801561001057600080fd5b50600436106101005760003560e01c806370a0823111610097578063a9059cbb11610066578063a9059cbb14610208578063ae1f6aaf1461021b578063c01e1bd614610246578063dd62ed3e1461025957600080fd5b806370a08231146101b157806395d89b41146101da5780639dc29fac146101e2578063a457c2d7146101f557600080fd5b806323b872dd116100d357806323b872dd14610167578063313ce5671461017a578063395093511461018957806340c10f191461019c57600080fd5b806301ffc9a71461010557806306fdde031461012d578063095ea7b31461014257806318160ddd14610155575b600080fd5b610118610113366004610b9b565b610292565b60405190151581526020015b60405180910390f35b6101356102f0565b6040516101249190610bcc565b610118610150366004610c3d565b610382565b6002545b604051908152602001610124565b610118610175366004610c67565b610398565b60405160128152602001610124565b610118610197366004610c3d565b610447565b6101af6101aa366004610c3d565b610483565b005b6101596101bf366004610ca3565b6001600160a01b031660009081526020819052604090205490565b61013561052e565b6101af6101f0366004610c3d565b61053d565b610118610203366004610c3d565b6105dc565b610118610216366004610c3d565b610675565b60065461022e906001600160a01b031681565b6040516001600160a01b039091168152602001610124565b60055461022e906001600160a01b031681565b610159610267366004610cbe565b6001600160a01b03918216600090815260016020908152604080832093909416825291909152205490565b60007f01ffc9a7a5cef8baa21ed3c5c0d7e23accb804b619e9333b597f47a0d84076e2631d1d8b6360e01b6001600160e01b031984166301ffc9a760e01b14806102e857506001600160e01b0319848116908216145b949350505050565b6060600380546102ff90610cf1565b80601f016020809104026020016040519081016040528092919081815260200182805461032b90610cf1565b80156103785780601f1061034d57610100808354040283529160200191610378565b820191906000526020600020905b81548152906001019060200180831161035b57829003601f168201915b5050505050905090565b600061038f338484610682565b50600192915050565b60006103a58484846107a7565b6001600160a01b03841660009081526001602090815260408083203384529091529020548281101561042f5760405162461bcd60e51b815260206004820152602860248201527f45524332303a207472616e7366657220616d6f756e74206578636565647320616044820152676c6c6f77616e636560c01b60648201526084015b60405180910390fd5b61043c8533858403610682565b506001949350505050565b3360008181526001602090815260408083206001600160a01b0387168452909152812054909161038f91859061047e908690610d42565b610682565b6006546001600160a01b031633146104dd5760405162461bcd60e51b815260206004820181905260248201527f4f6e6c79204c32204272696467652063616e206d696e7420616e64206275726e6044820152606401610426565b6104e78282610976565b816001600160a01b03167f0f6798a560793a54c3bcfe86a93cde1e73087d944c0ea20544137d41213968858260405161052291815260200190565b60405180910390a25050565b6060600480546102ff90610cf1565b6006546001600160a01b031633146105975760405162461bcd60e51b815260206004820181905260248201527f4f6e6c79204c32204272696467652063616e206d696e7420616e64206275726e6044820152606401610426565b6105a18282610a55565b816001600160a01b03167fcc16f5dbb4873280815c1ee09dbd06736cffcc184412cf7a71a0fdb75d397ca58260405161052291815260200190565b3360009081526001602090815260408083206001600160a01b03861684529091528120548281101561065e5760405162461bcd60e51b815260206004820152602560248201527f45524332303a2064656372656173656420616c6c6f77616e63652062656c6f77604482015264207a65726f60d81b6064820152608401610426565b61066b3385858403610682565b5060019392505050565b600061038f3384846107a7565b6001600160a01b0383166106e45760405162461bcd60e51b8152602060048201526024808201527f45524332303a20617070726f76652066726f6d20746865207a65726f206164646044820152637265737360e01b6064820152608401610426565b6001600160a01b0382166107455760405162461bcd60e51b815260206004820152602260248201527f45524332303a20617070726f766520746f20746865207a65726f206164647265604482015261737360f01b6064820152608401610426565b6001600160a01b0383811660008181526001602090815260408083209487168084529482529182902085905590518481527f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b92591015b60405180910390a3505050565b6001600160a01b03831661080b5760405162461bcd60e51b815260206004820152602560248201527f45524332303a207472616e736665722066726f6d20746865207a65726f206164604482015264647265737360d81b6064820152608401610426565b6001600160a01b03821661086d5760405162461bcd60e51b815260206004820152602360248201527f45524332303a207472616e7366657220746f20746865207a65726f206164647260448201526265737360e81b6064820152608401610426565b6001600160a01b038316600090815260208190526040902054818110156108e55760405162461bcd60e51b815260206004820152602660248201527f45524332303a207472616e7366657220616d6f756e7420657863656564732062604482015265616c616e636560d01b6064820152608401610426565b6001600160a01b0380851660009081526020819052604080822085850390559185168152908120805484929061091c908490610d42565b92505081905550826001600160a01b0316846001600160a01b03167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef8460405161096891815260200190565b60405180910390a350505050565b6001600160a01b0382166109cc5760405162461bcd60e51b815260206004820152601f60248201527f45524332303a206d696e7420746f20746865207a65726f2061646472657373006044820152606401610426565b80600260008282546109de9190610d42565b90915550506001600160a01b03821660009081526020819052604081208054839290610a0b908490610d42565b90915550506040518181526001600160a01b038316906000907fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef9060200160405180910390a35050565b6001600160a01b038216610ab55760405162461bcd60e51b815260206004820152602160248201527f45524332303a206275726e2066726f6d20746865207a65726f206164647265736044820152607360f81b6064820152608401610426565b6001600160a01b03821660009081526020819052604090205481811015610b295760405162461bcd60e51b815260206004820152602260248201527f45524332303a206275726e20616d6f756e7420657863656564732062616c616e604482015261636560f01b6064820152608401610426565b6001600160a01b0383166000908152602081905260408120838303905560028054849290610b58908490610d5a565b90915550506040518281526000906001600160a01b038516907fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef9060200161079a565b600060208284031215610bad57600080fd5b81356001600160e01b031981168114610bc557600080fd5b9392505050565b600060208083528351808285015260005b81811015610bf957858101830151858201604001528201610bdd565b81811115610c0b576000604083870101525b50601f01601f1916929092016040019392505050565b80356001600160a01b0381168114610c3857600080fd5b919050565b60008060408385031215610c5057600080fd5b610c5983610c21565b946020939093013593505050565b600080600060608486031215610c7c57600080fd5b610c8584610c21565b9250610c9360208501610c21565b9150604084013590509250925092565b600060208284031215610cb557600080fd5b610bc582610c21565b60008060408385031215610cd157600080fd5b610cda83610c21565b9150610ce860208401610c21565b90509250929050565b600181811c90821680610d0557607f821691505b60208210811415610d2657634e487b7160e01b600052602260045260246000fd5b50919050565b634e487b7160e01b600052601160045260246000fd5b60008219821115610d5557610d55610d2c565b500190565b600082821015610d6c57610d6c610d2c565b50039056fea2646970667358221220e4189d7a9690479e2c49877ed1f1c553dd1ddb00503deb6acefe33febad17d2264736f6c634300080b0033";
        public L2StandardERC20DeploymentBase() : base(BYTECODE) { }
        public L2StandardERC20DeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_l2Bridge", 1)]
        public virtual string L2Bridge { get; set; }
        [Parameter("address", "_l1Token", 2)]
        public virtual string L1Token { get; set; }
        [Parameter("string", "_name", 3)]
        public virtual string Name { get; set; }
        [Parameter("string", "_symbol", 4)]
        public virtual string Symbol { get; set; }
    }

    public partial class AllowanceFunction : AllowanceFunctionBase { }

    [Function("allowance", "uint256")]
    public class AllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
    }

    public partial class ApproveFunction : ApproveFunctionBase { }

    [Function("approve", "bool")]
    public class ApproveFunctionBase : FunctionMessage
    {
        [Parameter("address", "spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class BalanceOfFunction : BalanceOfFunctionBase { }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class BurnFunction : BurnFunctionBase { }

    [Function("burn")]
    public class BurnFunctionBase : FunctionMessage
    {
        [Parameter("address", "_from", 1)]
        public virtual string From { get; set; }
        [Parameter("uint256", "_amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class DecimalsFunction : DecimalsFunctionBase { }

    [Function("decimals", "uint8")]
    public class DecimalsFunctionBase : FunctionMessage
    {

    }

    public partial class DecreaseAllowanceFunction : DecreaseAllowanceFunctionBase { }

    [Function("decreaseAllowance", "bool")]
    public class DecreaseAllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "subtractedValue", 2)]
        public virtual BigInteger SubtractedValue { get; set; }
    }

    public partial class IncreaseAllowanceFunction : IncreaseAllowanceFunctionBase { }

    [Function("increaseAllowance", "bool")]
    public class IncreaseAllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "addedValue", 2)]
        public virtual BigInteger AddedValue { get; set; }
    }

    public partial class L1TokenFunction : L1TokenFunctionBase { }

    [Function("l1Token", "address")]
    public class L1TokenFunctionBase : FunctionMessage
    {

    }

    public partial class L2BridgeFunction : L2BridgeFunctionBase { }

    [Function("l2Bridge", "address")]
    public class L2BridgeFunctionBase : FunctionMessage
    {

    }

    public partial class MintFunction : MintFunctionBase { }

    [Function("mint")]
    public class MintFunctionBase : FunctionMessage
    {
        [Parameter("address", "_to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {

    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "_interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
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
        [Parameter("address", "recipient", 1)]
        public virtual string Recipient { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class TransferFromFunction : TransferFromFunctionBase { }

    [Function("transferFrom", "bool")]
    public class TransferFromFunctionBase : FunctionMessage
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("address", "recipient", 2)]
        public virtual string Recipient { get; set; }
        [Parameter("uint256", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class ApprovalEventDTO : ApprovalEventDTOBase { }

    [Event("Approval")]
    public class ApprovalEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true)]
        public virtual string Owner { get; set; }
        [Parameter("address", "spender", 2, true)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "value", 3, false)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class BurnEventDTO : BurnEventDTOBase { }

    [Event("Burn")]
    public class BurnEventDTOBase : IEventDTO
    {
        [Parameter("address", "_account", 1, true)]
        public virtual string Account { get; set; }
        [Parameter("uint256", "_amount", 2, false)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class MintEventDTO : MintEventDTOBase { }

    [Event("Mint")]
    public class MintEventDTOBase : IEventDTO
    {
        [Parameter("address", "_account", 1, true)]
        public virtual string Account { get; set; }
        [Parameter("uint256", "_amount", 2, false)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class TransferEventDTO : TransferEventDTOBase { }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("address", "from", 1, true)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2, true)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 3, false)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class AllowanceOutputDTO : AllowanceOutputDTOBase { }

    [FunctionOutput]
    public class AllowanceOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase { }

    [FunctionOutput]
    public class BalanceOfOutputDTOBase : IFunctionOutputDTO
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





    public partial class L1TokenOutputDTO : L1TokenOutputDTOBase { }

    [FunctionOutput]
    public class L1TokenOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class L2BridgeOutputDTO : L2BridgeOutputDTOBase { }

    [FunctionOutput]
    public class L2BridgeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
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
