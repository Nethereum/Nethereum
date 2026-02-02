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
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestExecAccount.ContractDefinition
{


    public partial class TestExecAccountDeployment : TestExecAccountDeploymentBase
    {
        public TestExecAccountDeployment() : base(BYTECODE) { }
        public TestExecAccountDeployment(string byteCode) : base(byteCode) { }
    }

    public class TestExecAccountDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "60c03461014757601f61150838819003918201601f19168301916001600160401b0383118484101761014b5780849260209460405283398101031261014757516001600160a01b0381168103610147573060805260a0525f5160206114e85f395f51905f525460ff8160401c16610138576002600160401b03196001600160401b038216016100e2575b6040516113889081610160823960805181818161081701526108aa015260a051818181610179015281816102a3015281816103c80152818161053301528181610a7901528181610b1301528181610ca901526110860152f35b6001600160401b0319166001600160401b039081175f5160206114e85f395f51905f52556040519081527fc7f505b2f371ae2175ee4913f4499e1f2633a7b5936321eed1cdaeb6115181d290602090a15f610089565b63f92ee8a960e01b5f5260045ffd5b5f80fd5b634e487b7160e01b5f52604160045260245ffdfe608080604052600436101561001c575b50361561001a575f80fd5b005b5f905f3560e01c90816301ffc9a714610dac57508063150b7a0214610d5657806319822f7c14610c7357806334fcd5be14610b865780634a58db1914610b055780634d44560d14610a545780634f1ef2861461086b57806352d1902d146108045780638da5cb5b146107dd5780638dd7712f146105af578063ad3cb1cc14610562578063b0d691fe1461051d578063b61d27f6146104a2578063bc197c8114610409578063c399ec881461039b578063c4d66de8146101fe578063d087d288146101455763f23a6e610361000f57346101425760a036600319011261014257610103610e17565b5061010c610e2d565b506084356001600160401b0381116101405761012c903690600401610e57565b505060405163f23a6e6160e01b8152602090f35b505b80fd5b5034610142578060031936011261014257604051631aab3f0d60e11b815230600482015260248101829052906020826044817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa9081156101f257906101bb575b602090604051908152f35b506020813d6020116101ea575b816101d560209383610eb4565b810103126101e657602090516101b0565b5f80fd5b3d91506101c8565b604051903d90823e3d90fd5b503461014257602036600319011261014257610218610e17565b5f5160206113335f395f51905f52549060ff8260401c1615916001600160401b03811680159081610393575b6001149081610389575b159081610380575b506103715767ffffffffffffffff1981166001175f5160206113335f395f51905f525582610345575b5082546001600160a01b0319166001600160a01b03918216908117845560405192917f0000000000000000000000000000000000000000000000000000000000000000167f47e55c76e7a6f1fd8996a1da8008c1ea29699cca35e7bcd057f2dec313b6e5de8580a36102ef575080f35b60207fc7f505b2f371ae2175ee4913f4499e1f2633a7b5936321eed1cdaeb6115181d29168ff0000000000000000195f5160206113335f395f51905f5254165f5160206113335f395f51905f525560018152a180f35b68ffffffffffffffffff191668010000000000000001175f5160206113335f395f51905f52555f61027f565b63f92ee8a960e01b8452600484fd5b9050155f610256565b303b15915061024e565b849150610244565b50346101425780600319360112610142576040516370a0823160e01b8152306004820152906020826024817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa9081156101f257906101bb57602090604051908152f35b50346101425760a036600319011261014257610423610e17565b5061042c610e2d565b506044356001600160401b0381116101405761044c903690600401610e84565b50506064356001600160401b0381116101405761046d903690600401610e84565b50506084356001600160401b0381116101405761048e903690600401610e57565b505060405163bc197c8160e01b8152602090f35b503461014257606036600319011261014257806104bd610e17565b6044356001600160401b0381116105195782916104e16104f4923690600401610e57565b92906104eb611083565b5a933691610f04565b916020835193019160243591f1156105095780f35b610511611114565b602081519101fd5b5050fd5b50346101425780600319360112610142576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b5034610142578060031936011261014257506105ab604051610585604082610eb4565b60058152640352e302e360dc1b6020820152604051918291602083526020830190610f58565b0390f35b5034610142576040366003190112610142576004356001600160401b038111610140578060040161012060031983360301126107d9576105ed611083565b8260648301916105fd8382610f7c565b929083600411610140576060936003198101610726575b827fd3fddfd1276d1cc278f10907710a44474a32f917b2fcfa198f46ca7689215e2f6106b888610720896107128d6106ff8c6101046106f7604051998a9960408b5260018060a01b0361066686610e43565b1660408c0152602487013560608c01526106a661069f8c61016061068d60448c018a610fdd565b9190926101206080820152019161100e565b9186610fdd565b8c8303603f190160a08e01529061100e565b608486013560c08b015260a486013560e08b015260c48601356101008b01526106e460e4870185610fdd565b8b8303603f19016101208d01529061100e565b930190610fdd565b868303603f19016101408801529061100e565b908382036020850152610f58565b0390a180f35b9294935090918101604082820360031901126107d95761074860048301610e43565b906024830135906001600160401b0382116107d557600461076c9286950101610f3a565b80519160209091019083906001600160a01b03165af19261078b610fae565b931561079c57929091845f80610614565b60405162461bcd60e51b81526020600482015260116024820152701a5b9b995c8818d85b1b0819985a5b1959607a1b6044820152606490fd5b8480fd5b8280fd5b5034610142578060031936011261014257546040516001600160a01b039091168152602090f35b50346101425780600319360112610142577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316300361085c5760206040515f5160206113135f395f51905f528152f35b63703e46dd60e11b8152600490fd5b50604036600319011261014257610880610e17565b906024356001600160401b038111610140576108a0903690600401610f3a565b6001600160a01b037f000000000000000000000000000000000000000000000000000000000000000016308114908115610a32575b50610a23576108e261112e565b6040516352d1902d60e01b8152926001600160a01b0381169190602085600481865afa809585966109ef575b5061092757634c9c8ce360e01b84526004839052602484fd5b9091845f5160206113135f395f51905f5281036109dd5750813b156109cb575f5160206113135f395f51905f5280546001600160a01b031916821790557fbc7cd75a20ee27fd9adebab32041f755214dbc6bffa90cc0225b39da2e5c2d3b8480a281518390156109b157808360206109ad95519101845af46109a7610fae565b916112b4565b5080f35b505050346109bc5780f35b63b398979f60e01b8152600490fd5b634c9c8ce360e01b8452600452602483fd5b632a87526960e21b8552600452602484fd5b9095506020813d602011610a1b575b81610a0b60209383610eb4565b810103126107d55751945f61090e565b3d91506109fe565b63703e46dd60e11b8252600482fd5b5f5160206113135f395f51905f52546001600160a01b0316141590505f6108d5565b50346101425760403660031901126101425780610a6f610e17565b610a7761112e565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690813b156105195760405163040b850f60e31b81526001600160a01b03909116600482015260248035908201529082908290604490829084905af18015610afa57610ae95750f35b81610af391610eb4565b6101425780f35b6040513d84823e3d90fd5b505f3660031901126101e6577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316803b156101e6575f6024916040519283809263b760faf960e01b825230600483015234905af18015610b7b57610b6f575080f35b61001a91505f90610eb4565b6040513d5f823e3d90fd5b346101e65760203660031901126101e6576004356001600160401b0381116101e657610bb6903690600401610e84565b610bbe611083565b36829003605e19015f5b8281101561001a578060051b840135828112156101e65784018035906001600160a01b03821682036101e6575f9181610c13610c08604086950183610f7c565b91905a923691610f04565b926020808551950193013591f115610c2d57600101610bc8565b60018303610c3d57610511611114565b610c45611114565b90610c6f604051928392635a15467560e01b84526004840152604060248401526044830190610f58565b0390fd5b346101e65760603660031901126101e6576004356001600160401b0381116101e65761012060031982360301126101e6576044357f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03163303610d1157610ce96020926024359060040161102e565b9080610cf9575b50604051908152f35b5f80808093335af150610d0a610fae565b5082610cf0565b60405162461bcd60e51b815260206004820152601c60248201527f6163636f756e743a206e6f742066726f6d20456e747279506f696e74000000006044820152606490fd5b346101e65760803660031901126101e657610d6f610e17565b50610d78610e2d565b506064356001600160401b0381116101e657610d98903690600401610e57565b5050604051630a85bd0160e11b8152602090f35b346101e65760203660031901126101e6576004359063ffffffff60e01b82168092036101e657602091630a85bd0160e11b8114908115610e06575b8115610df5575b5015158152f35b6301ffc9a760e01b14905083610dee565b630271189760e51b81149150610de7565b600435906001600160a01b03821682036101e657565b602435906001600160a01b03821682036101e657565b35906001600160a01b03821682036101e657565b9181601f840112156101e6578235916001600160401b0383116101e657602083818601950101116101e657565b9181601f840112156101e6578235916001600160401b0383116101e6576020808501948460051b0101116101e657565b90601f801991011681019081106001600160401b03821117610ed557604052565b634e487b7160e01b5f52604160045260245ffd5b6001600160401b038111610ed557601f01601f191660200190565b929192610f1082610ee9565b91610f1e6040519384610eb4565b8294818452818301116101e6578281602093845f960137010152565b9080601f830112156101e657816020610f5593359101610f04565b90565b805180835260209291819084018484015e5f828201840152601f01601f1916010190565b903590601e19813603018212156101e657018035906001600160401b0382116101e6576020019181360383136101e657565b3d15610fd8573d90610fbf82610ee9565b91610fcd6040519384610eb4565b82523d5f602084013e565b606090565b9035601e19823603018112156101e65701602081359101916001600160401b0382116101e65781360383136101e657565b908060209392818452848401375f828201840152601f01601f1916010190565b5f546001600160a01b03169161106c91611063919061105d9061105690610100810190610f7c565b3691610f04565b90611184565b909291926111be565b6001600160a01b03160361107e575f90565b600190565b337f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316148015611101575b156110bd57565b606460405162461bcd60e51b815260206004820152602060248201527f6163636f756e743a206e6f74204f776e6572206f7220456e747279506f696e746044820152fd5b505f546001600160a01b031633146110b6565b3d604051906020818301016040528082525f602083013e90565b5f546001600160a01b03163314801561117b575b1561114957565b60405162461bcd60e51b815260206004820152600a60248201526937b7363c9037bbb732b960b11b6044820152606490fd5b50303314611142565b81519190604183036111b4576111ad9250602082015190606060408401519301515f1a90611232565b9192909190565b50505f9160029190565b600481101561121e57806111d0575050565b600181036111e75763f645eedf60e01b5f5260045ffd5b60028103611202575063fce698f760e01b5f5260045260245ffd5b60031461120c5750565b6335e2f38360e21b5f5260045260245ffd5b634e487b7160e01b5f52602160045260245ffd5b91907f7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a084116112a9579160209360809260ff5f9560405194855216868401526040830152606082015282805260015afa15610b7b575f516001600160a01b0381161561129f57905f905f90565b505f906001905f90565b5050505f9160039190565b906112d857508051156112c957805190602001fd5b63d6bda27560e01b5f5260045ffd5b81511580611309575b6112e9575090565b639996b31560e01b5f9081526001600160a01b0391909116600452602490fd5b50803b156112e156fe360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbcf0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a00a26469706673582212203494528b51e42f4fdea92bd02057c7a506ed47ece25deb7807140117ffabdef564736f6c634300081d0033f0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a00";
        public TestExecAccountDeploymentBase() : base(BYTECODE) { }
        public TestExecAccountDeploymentBase(string byteCode) : base(byteCode) { }
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

    public partial class ExecuteUserOpFunction : ExecuteUserOpFunctionBase { }

    [Function("executeUserOp")]
    public class ExecuteUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "", 2)]
        public virtual byte[] ReturnValue2 { get; set; }
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

    public partial class ExecutedEventDTO : ExecutedEventDTOBase { }

    [Event("Executed")]
    public class ExecutedEventDTOBase : IEventDTO
    {
        [Parameter("tuple", "userOp", 1, false )]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes", "innerCallRet", 2, false )]
        public virtual byte[] InnerCallRet { get; set; }
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
