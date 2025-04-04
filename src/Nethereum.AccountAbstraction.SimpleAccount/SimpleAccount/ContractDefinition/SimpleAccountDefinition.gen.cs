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

namespace Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition
{


    public partial class SimpleAccountDeployment : SimpleAccountDeploymentBase
    {
        public SimpleAccountDeployment() : base(BYTECODE) { }
        public SimpleAccountDeployment(string byteCode) : base(byteCode) { }
    }

    public class SimpleAccountDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "60c03461014757601f61126538819003918201601f19168301916001600160401b0383118484101761014b5780849260209460405283398101031261014757516001600160a01b0381168103610147573060805260a0525f5160206112455f395f51905f525460ff8160401c16610138576002600160401b03196001600160401b038216016100e2575b6040516110e5908161016082396080518181816105de0152610682015260a05181818161016e01528181610298015281816103bd0152818161052801528181610862015281816108f301528181610a890152610de30152f35b6001600160401b0319166001600160401b039081175f5160206112455f395f51905f52556040519081527fc7f505b2f371ae2175ee4913f4499e1f2633a7b5936321eed1cdaeb6115181d290602090a15f610089565b63f92ee8a960e01b5f5260045ffd5b5f80fd5b634e487b7160e01b5f52604160045260245ffdfe608080604052600436101561001c575b50361561001a575f80fd5b005b5f905f3560e01c90816301ffc9a714610b8c57508063150b7a0214610b3657806319822f7c14610a5357806334fcd5be146109665780634a58db19146108e55780634d44560d146108305780634f1ef2861461063257806352d1902d146105cb5780638da5cb5b146105a4578063ad3cb1cc14610557578063b0d691fe14610512578063b61d27f614610497578063bc197c81146103fe578063c399ec8814610390578063c4d66de8146101f3578063d087d2881461013a5763f23a6e610361000f57346101375760a0366003190112610137576100f8610bf7565b50610101610c0d565b506084356001600160401b03811161013557610121903690600401610c23565b505060405163f23a6e6160e01b8152602090f35b505b80fd5b5034610137578060031936011261013757604051631aab3f0d60e11b815230600482015260248101829052906020826044817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa9081156101e757906101b0575b602090604051908152f35b506020813d6020116101df575b816101ca60209383610c80565b810103126101db57602090516101a5565b5f80fd5b3d91506101bd565b604051903d90823e3d90fd5b50346101375760203660031901126101375761020d610bf7565b5f5160206110905f395f51905f52549060ff8260401c1615916001600160401b03811680159081610388575b600114908161037e575b159081610375575b506103665767ffffffffffffffff1981166001175f5160206110905f395f51905f52558261033a575b5082546001600160a01b0319166001600160a01b03918216908117845560405192917f0000000000000000000000000000000000000000000000000000000000000000167f47e55c76e7a6f1fd8996a1da8008c1ea29699cca35e7bcd057f2dec313b6e5de8580a36102e4575080f35b60207fc7f505b2f371ae2175ee4913f4499e1f2633a7b5936321eed1cdaeb6115181d29168ff0000000000000000195f5160206110905f395f51905f5254165f5160206110905f395f51905f525560018152a180f35b68ffffffffffffffffff191668010000000000000001175f5160206110905f395f51905f52555f610274565b63f92ee8a960e01b8452600484fd5b9050155f61024b565b303b159150610243565b849150610239565b50346101375780600319360112610137576040516370a0823160e01b8152306004820152906020826024817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa9081156101e757906101b057602090604051908152f35b50346101375760a036600319011261013757610418610bf7565b50610421610c0d565b506044356001600160401b03811161013557610441903690600401610c50565b50506064356001600160401b03811161013557610462903690600401610c50565b50506084356001600160401b03811161013557610483903690600401610c23565b505060405163bc197c8160e01b8152602090f35b503461013757606036600319011261013757806104b2610bf7565b6044356001600160401b03811161050e5782916104d66104e9923690600401610c23565b92906104e0610de0565b5a933691610cd0565b916020835193019160243591f1156104fe5780f35b610506610e71565b602081519101fd5b5050fd5b50346101375780600319360112610137576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b5034610137578060031936011261013757506105a060405161057a604082610c80565b60058152640352e302e360dc1b6020820152604051918291602083526020830190610d06565b0390f35b5034610137578060031936011261013757546040516001600160a01b039091168152602090f35b50346101375780600319360112610137577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031630036106235760206040515f5160206110705f395f51905f528152f35b63703e46dd60e11b8152600490fd5b50604036600319011261013757610647610bf7565b906024356001600160401b038111610135573660238201121561013557610678903690602481600401359101610cd0565b6001600160a01b037f00000000000000000000000000000000000000000000000000000000000000001630811490811561080e575b506107ff576106ba610e8b565b6040516352d1902d60e01b8152926001600160a01b0381169190602085600481865afa809585966107c7575b506106ff57634c9c8ce360e01b84526004839052602484fd5b9091845f5160206110705f395f51905f5281036107b55750813b156107a3575f5160206110705f395f51905f5280546001600160a01b031916821790557fbc7cd75a20ee27fd9adebab32041f755214dbc6bffa90cc0225b39da2e5c2d3b8480a28151839015610789578083602061078595519101845af461077f610db1565b91611011565b5080f35b505050346107945780f35b63b398979f60e01b8152600490fd5b634c9c8ce360e01b8452600452602483fd5b632a87526960e21b8552600452602484fd5b9095506020813d6020116107f7575b816107e360209383610c80565b810103126107f35751945f6106e6565b8480fd5b3d91506107d6565b63703e46dd60e11b8252600482fd5b5f5160206110705f395f51905f52546001600160a01b0316141590505f6106ad565b503461013757604036600319011261013757806004356001600160a01b038116908190036108e257610860610e8b565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690813b1561050e57829160448392604051948593849263040b850f60e31b8452600484015260243560248401525af180156108d7576108c65750f35b816108d091610c80565b6101375780f35b6040513d84823e3d90fd5b50fd5b505f3660031901126101db577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316803b156101db575f6024916040519283809263b760faf960e01b825230600483015234905af1801561095b5761094f575080f35b61001a91505f90610c80565b6040513d5f823e3d90fd5b346101db5760203660031901126101db576004356001600160401b0381116101db57610996903690600401610c50565b61099e610de0565b36829003605e19015f5b8281101561001a578060051b840135828112156101db5784018035906001600160a01b03821682036101db575f91816109f36109e8604086950183610d2a565b91905a923691610cd0565b926020808551950193013591f115610a0d576001016109a8565b60018303610a1d57610506610e71565b610a25610e71565b90610a4f604051928392635a15467560e01b84526004840152604060248401526044830190610d06565b0390fd5b346101db5760603660031901126101db576004356001600160401b0381116101db5761012060031982360301126101db576044357f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03163303610af157610ac960209260243590600401610d5c565b9080610ad9575b50604051908152f35b5f80808093335af150610aea610db1565b5082610ad0565b60405162461bcd60e51b815260206004820152601c60248201527f6163636f756e743a206e6f742066726f6d20456e747279506f696e74000000006044820152606490fd5b346101db5760803660031901126101db57610b4f610bf7565b50610b58610c0d565b506064356001600160401b0381116101db57610b78903690600401610c23565b5050604051630a85bd0160e11b8152602090f35b346101db5760203660031901126101db576004359063ffffffff60e01b82168092036101db57602091630a85bd0160e11b8114908115610be6575b8115610bd5575b5015158152f35b6301ffc9a760e01b14905083610bce565b630271189760e51b81149150610bc7565b600435906001600160a01b03821682036101db57565b602435906001600160a01b03821682036101db57565b9181601f840112156101db578235916001600160401b0383116101db57602083818601950101116101db57565b9181601f840112156101db578235916001600160401b0383116101db576020808501948460051b0101116101db57565b90601f801991011681019081106001600160401b03821117610ca157604052565b634e487b7160e01b5f52604160045260245ffd5b6001600160401b038111610ca157601f01601f191660200190565b929192610cdc82610cb5565b91610cea6040519384610c80565b8294818452818301116101db578281602093845f960137010152565b805180835260209291819084018484015e5f828201840152601f01601f1916010190565b903590601e19813603018212156101db57018035906001600160401b0382116101db576020019181360383136101db57565b5f546001600160a01b031691610d9a91610d919190610d8b90610d8490610100810190610d2a565b3691610cd0565b90610ee1565b90929192610f1b565b6001600160a01b031603610dac575f90565b600190565b3d15610ddb573d90610dc282610cb5565b91610dd06040519384610c80565b82523d5f602084013e565b606090565b337f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316148015610e5e575b15610e1a57565b606460405162461bcd60e51b815260206004820152602060248201527f6163636f756e743a206e6f74204f776e6572206f7220456e747279506f696e746044820152fd5b505f546001600160a01b03163314610e13565b3d604051906020818301016040528082525f602083013e90565b5f546001600160a01b031633148015610ed8575b15610ea657565b60405162461bcd60e51b815260206004820152600a60248201526937b7363c9037bbb732b960b11b6044820152606490fd5b50303314610e9f565b8151919060418303610f1157610f0a9250602082015190606060408401519301515f1a90610f8f565b9192909190565b50505f9160029190565b6004811015610f7b5780610f2d575050565b60018103610f445763f645eedf60e01b5f5260045ffd5b60028103610f5f575063fce698f760e01b5f5260045260245ffd5b600314610f695750565b6335e2f38360e21b5f5260045260245ffd5b634e487b7160e01b5f52602160045260245ffd5b91907f7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a08411611006579160209360809260ff5f9560405194855216868401526040830152606082015282805260015afa1561095b575f516001600160a01b03811615610ffc57905f905f90565b505f906001905f90565b5050505f9160039190565b90611035575080511561102657805190602001fd5b63d6bda27560e01b5f5260045ffd5b81511580611066575b611046575090565b639996b31560e01b5f9081526001600160a01b0391909116600452602490fd5b50803b1561103e56fe360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbcf0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a00a264697066735822122081fd1357b120026896a4a48fd727b7bee449d49687e3f19ea9931fdc9c23c7f664736f6c634300081d0033f0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a00";
        public SimpleAccountDeploymentBase() : base(BYTECODE) { }
        public SimpleAccountDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "anEntryPoint", 1)]
        public virtual string AnEntryPoint { get; set; }
    }

    public partial class UpgradeInterfaceVersionFunction : UpgradeInterfaceVersionFunctionBase { }

    [Function("UPGRADE_INTERFACE_VERSION", "string")]
    public class UpgradeInterfaceVersionFunctionBase : FunctionMessage
    {

    }

    public partial class AddDepositFunction : AddDepositFunctionBase { }

    [Function("addDeposit")]
    public class AddDepositFunctionBase : FunctionMessage
    {

    }

    public partial class EntryPointFunction : EntryPointFunctionBase { }

    [Function("entryPoint", "address")]
    public class EntryPointFunctionBase : FunctionMessage
    {

    }

    public partial class ExecuteFunction : ExecuteFunctionBase { }

    [Function("execute")]
    public class ExecuteFunctionBase : FunctionMessage
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class ExecuteBatchFunction : ExecuteBatchFunctionBase { }

    [Function("executeBatch")]
    public class ExecuteBatchFunctionBase : FunctionMessage
    {
        [Parameter("tuple[]", "calls", 1)]
        public virtual List<Call> Calls { get; set; }
    }

    public partial class GetDepositFunction : GetDepositFunctionBase { }

    [Function("getDeposit", "uint256")]
    public class GetDepositFunctionBase : FunctionMessage
    {

    }

    public partial class GetNonceFunction : GetNonceFunctionBase { }

    [Function("getNonce", "uint256")]
    public class GetNonceFunctionBase : FunctionMessage
    {

    }

    public partial class InitializeFunction : InitializeFunctionBase { }

    [Function("initialize")]
    public class InitializeFunctionBase : FunctionMessage
    {
        [Parameter("address", "anOwner", 1)]
        public virtual string AnOwner { get; set; }
    }

    public partial class OnERC1155BatchReceivedFunction : OnERC1155BatchReceivedFunctionBase { }

    [Function("onERC1155BatchReceived", "bytes4")]
    public class OnERC1155BatchReceivedFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
        [Parameter("uint256[]", "", 3)]
        public virtual List<BigInteger> ReturnValue3 { get; set; }
        [Parameter("uint256[]", "", 4)]
        public virtual List<BigInteger> ReturnValue4 { get; set; }
        [Parameter("bytes", "", 5)]
        public virtual byte[] ReturnValue5 { get; set; }
    }

    public partial class OnERC1155ReceivedFunction : OnERC1155ReceivedFunctionBase { }

    [Function("onERC1155Received", "bytes4")]
    public class OnERC1155ReceivedFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
        [Parameter("uint256", "", 3)]
        public virtual BigInteger ReturnValue3 { get; set; }
        [Parameter("uint256", "", 4)]
        public virtual BigInteger ReturnValue4 { get; set; }
        [Parameter("bytes", "", 5)]
        public virtual byte[] ReturnValue5 { get; set; }
    }

    public partial class OnERC721ReceivedFunction : OnERC721ReceivedFunctionBase { }

    [Function("onERC721Received", "bytes4")]
    public class OnERC721ReceivedFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
        [Parameter("uint256", "", 3)]
        public virtual BigInteger ReturnValue3 { get; set; }
        [Parameter("bytes", "", 4)]
        public virtual byte[] ReturnValue4 { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class ProxiableUUIDFunction : ProxiableUUIDFunctionBase { }

    [Function("proxiableUUID", "bytes32")]
    public class ProxiableUUIDFunctionBase : FunctionMessage
    {

    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class UpgradeToAndCallFunction : UpgradeToAndCallFunctionBase { }

    [Function("upgradeToAndCall")]
    public class UpgradeToAndCallFunctionBase : FunctionMessage
    {
        [Parameter("address", "newImplementation", 1)]
        public virtual string NewImplementation { get; set; }
        [Parameter("bytes", "data", 2)]
        public virtual byte[] Data { get; set; }
    }

    public partial class ValidateUserOpFunction : ValidateUserOpFunctionBase { }

    [Function("validateUserOp", "uint256")]
    public class ValidateUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
        [Parameter("uint256", "missingAccountFunds", 3)]
        public virtual BigInteger MissingAccountFunds { get; set; }
    }

    public partial class WithdrawDepositToFunction : WithdrawDepositToFunctionBase { }

    [Function("withdrawDepositTo")]
    public class WithdrawDepositToFunctionBase : FunctionMessage
    {
        [Parameter("address", "withdrawAddress", 1)]
        public virtual string WithdrawAddress { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class InitializedEventDTO : InitializedEventDTOBase { }

    [Event("Initialized")]
    public class InitializedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "version", 1, false )]
        public virtual ulong Version { get; set; }
    }

    public partial class SimpleAccountInitializedEventDTO : SimpleAccountInitializedEventDTOBase { }

    [Event("SimpleAccountInitialized")]
    public class SimpleAccountInitializedEventDTOBase : IEventDTO
    {
        [Parameter("address", "entryPoint", 1, true )]
        public virtual string EntryPoint { get; set; }
        [Parameter("address", "owner", 2, true )]
        public virtual string Owner { get; set; }
    }

    public partial class UpgradedEventDTO : UpgradedEventDTOBase { }

    [Event("Upgraded")]
    public class UpgradedEventDTOBase : IEventDTO
    {
        [Parameter("address", "implementation", 1, true )]
        public virtual string Implementation { get; set; }
    }

    public partial class AddressEmptyCodeError : AddressEmptyCodeErrorBase { }

    [Error("AddressEmptyCode")]
    public class AddressEmptyCodeErrorBase : IErrorDTO
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
    }

    public partial class ECDSAInvalidSignatureError : ECDSAInvalidSignatureErrorBase { }
    [Error("ECDSAInvalidSignature")]
    public class ECDSAInvalidSignatureErrorBase : IErrorDTO
    {
    }

    public partial class ECDSAInvalidSignatureLengthError : ECDSAInvalidSignatureLengthErrorBase { }

    [Error("ECDSAInvalidSignatureLength")]
    public class ECDSAInvalidSignatureLengthErrorBase : IErrorDTO
    {
        [Parameter("uint256", "length", 1)]
        public virtual BigInteger Length { get; set; }
    }

    public partial class ECDSAInvalidSignatureSError : ECDSAInvalidSignatureSErrorBase { }

    [Error("ECDSAInvalidSignatureS")]
    public class ECDSAInvalidSignatureSErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "s", 1)]
        public virtual byte[] S { get; set; }
    }

    public partial class ERC1967InvalidImplementationError : ERC1967InvalidImplementationErrorBase { }

    [Error("ERC1967InvalidImplementation")]
    public class ERC1967InvalidImplementationErrorBase : IErrorDTO
    {
        [Parameter("address", "implementation", 1)]
        public virtual string Implementation { get; set; }
    }

    public partial class ERC1967NonPayableError : ERC1967NonPayableErrorBase { }
    [Error("ERC1967NonPayable")]
    public class ERC1967NonPayableErrorBase : IErrorDTO
    {
    }

    public partial class ExecuteErrorError : ExecuteErrorErrorBase { }

    [Error("ExecuteError")]
    public class ExecuteErrorErrorBase : IErrorDTO
    {
        [Parameter("uint256", "index", 1)]
        public virtual BigInteger Index { get; set; }
        [Parameter("bytes", "error", 2)]
        public virtual byte[] Error { get; set; }
    }

    public partial class FailedCallError : FailedCallErrorBase { }
    [Error("FailedCall")]
    public class FailedCallErrorBase : IErrorDTO
    {
    }

    public partial class InvalidInitializationError : InvalidInitializationErrorBase { }
    [Error("InvalidInitialization")]
    public class InvalidInitializationErrorBase : IErrorDTO
    {
    }

    public partial class NotInitializingError : NotInitializingErrorBase { }
    [Error("NotInitializing")]
    public class NotInitializingErrorBase : IErrorDTO
    {
    }

    public partial class UUPSUnauthorizedCallContextError : UUPSUnauthorizedCallContextErrorBase { }
    [Error("UUPSUnauthorizedCallContext")]
    public class UUPSUnauthorizedCallContextErrorBase : IErrorDTO
    {
    }

    public partial class UUPSUnsupportedProxiableUUIDError : UUPSUnsupportedProxiableUUIDErrorBase { }

    [Error("UUPSUnsupportedProxiableUUID")]
    public class UUPSUnsupportedProxiableUUIDErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "slot", 1)]
        public virtual byte[] Slot { get; set; }
    }

    public partial class UpgradeInterfaceVersionOutputDTO : UpgradeInterfaceVersionOutputDTOBase { }

    [FunctionOutput]
    public class UpgradeInterfaceVersionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class EntryPointOutputDTO : EntryPointOutputDTOBase { }

    [FunctionOutput]
    public class EntryPointOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }





    public partial class GetDepositOutputDTO : GetDepositOutputDTOBase { }

    [FunctionOutput]
    public class GetDepositOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetNonceOutputDTO : GetNonceOutputDTOBase { }

    [FunctionOutput]
    public class GetNonceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class OnERC1155BatchReceivedOutputDTO : OnERC1155BatchReceivedOutputDTOBase { }

    [FunctionOutput]
    public class OnERC1155BatchReceivedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes4", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class OnERC1155ReceivedOutputDTO : OnERC1155ReceivedOutputDTOBase { }

    [FunctionOutput]
    public class OnERC1155ReceivedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes4", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class OnERC721ReceivedOutputDTO : OnERC721ReceivedOutputDTOBase { }

    [FunctionOutput]
    public class OnERC721ReceivedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes4", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ProxiableUUIDOutputDTO : ProxiableUUIDOutputDTOBase { }

    [FunctionOutput]
    public class ProxiableUUIDOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }






}
