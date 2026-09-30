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
        public static string BYTECODE = "60c03461014757601f611a0938819003918201601f19168301916001600160401b0383118484101761014b5780849260209460405283398101031261014757516001600160a01b0381168103610147573060805260a0525f5160206119e95f395f51905f525460ff8160401c16610138576002600160401b03196001600160401b038216016100e2575b604051611889908161016082396080518181816108c401526109b8015260a0518181816101f0015281816103a7015281816105960152818161078601528181610cf501528181610dca0152818161102601526114ea0152f35b6001600160401b0319166001600160401b039081175f5160206119e95f395f51905f52556040519081527fc7f505b2f371ae2175ee4913f4499e1f2633a7b5936321eed1cdaeb6115181d290602090a15f610089565b63f92ee8a960e01b5f5260045ffd5b5f80fd5b634e487b7160e01b5f52604160045260245ffdfe608080604052600436101561001c575b50361561001a575f80fd5b005b5f905f3560e01c90816301ffc9a71461114857508063150b7a02146110bb57806319822f7c14610f9e57806334fcd5be14610e4e5780634a58db1914610d895780634d44560d14610c845780634f1ef2861461093c57806352d1902d1461087e5780638da5cb5b1461082d578063ad3cb1cc146107aa578063b0d691fe1461073b578063b61d27f6146106a1578063bc197c81146105cf578063c399ec881461051d578063c4d66de81461026d578063d087d288146101715763f23a6e610361000f573461016e5760a07ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e57610116611235565b5061011f611258565b5060843567ffffffffffffffff811161016c5761014090369060040161127b565b505060206040517ff23a6e61000000000000000000000000000000000000000000000000000000008152f35b505b80fd5b503461016e57807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e57604051907f35567e1a00000000000000000000000000000000000000000000000000000000825230600483015280602483015260208260448173ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000165afa908115610261579061022a575b602090604051908152f35b506020813d602011610259575b81610244602093836112da565b81010312610255576020905161021f565b5f80fd5b3d9150610237565b604051903d90823e3d90fd5b503461016e5760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e576102a5611235565b7ff0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a00549060ff8260401c16159167ffffffffffffffff811680159081610515575b600114908161050b575b159081610502575b506104da5790818360017fffffffffffffffffffffffffffffffffffffffffffffffff000000000000000073ffffffffffffffffffffffffffffffffffffffff9516177ff0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a0055610485575b501690817fffffffffffffffffffffffff00000000000000000000000000000000000000008454161783556040519173ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000167f47e55c76e7a6f1fd8996a1da8008c1ea29699cca35e7bcd057f2dec313b6e5de8580a36103f3575080f35b60207fc7f505b2f371ae2175ee4913f4499e1f2633a7b5936321eed1cdaeb6115181d2917fffffffffffffffffffffffffffffffffffffffffffffff00ffffffffffffffff7ff0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a0054167ff0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a005560018152a180f35b7fffffffffffffffffffffffffffffffffffffffffffffff0000000000000000001668010000000000000001177ff0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a00555f610361565b6004847ff92ee8a9000000000000000000000000000000000000000000000000000000008152fd5b9050155f6102f7565b303b1591506102ef565b8491506102e5565b503461016e57807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e57604051907f70a0823100000000000000000000000000000000000000000000000000000000825230600483015260208260248173ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000165afa908115610261579061022a57602090604051908152f35b503461016e5760a07ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e57610607611235565b50610610611258565b5060443567ffffffffffffffff811161016c576106319036906004016112a9565b505060643567ffffffffffffffff811161016c576106539036906004016112a9565b505060843567ffffffffffffffff811161016c5761067590369060040161127b565b505060206040517fbc197c81000000000000000000000000000000000000000000000000000000008152f35b503461016e5760607ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e57806106da611235565b60443567ffffffffffffffff81116107375782916106ff61071292369060040161127b565b92906107096114d3565b5a933691611382565b916020835193019160243591f1156107275780f35b61072f611599565b602081519101fd5b5050fd5b503461016e57807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e57602060405173ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000168152f35b503461016e57807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e57506108296040516107eb6040826112da565b600581527f352e302e3000000000000000000000000000000000000000000000000000000060208201526040519182916020835260208301906113b8565b0390f35b503461016e57807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e5773ffffffffffffffffffffffffffffffffffffffff6020915416604051908152f35b503461016e57807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e5773ffffffffffffffffffffffffffffffffffffffff7f00000000000000000000000000000000000000000000000000000000000000001630036109145760206040517f360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc8152f35b807fe07c8dba0000000000000000000000000000000000000000000000000000000060049252fd5b5060407ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e5761096f611235565b9060243567ffffffffffffffff811161016c573660238201121561016c576109a1903690602481600401359101611382565b73ffffffffffffffffffffffffffffffffffffffff7f000000000000000000000000000000000000000000000000000000000000000016803014908115610c42575b50610c1a576109f06115b3565b73ffffffffffffffffffffffffffffffffffffffff831690604051937f52d1902d000000000000000000000000000000000000000000000000000000008552602085600481865afa80958596610be2575b50610a7257602484847f4c9c8ce3000000000000000000000000000000000000000000000000000000008252600452fd5b9091847f360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc8103610bb75750813b15610b8c57807fffffffffffffffffffffffff00000000000000000000000000000000000000007f360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc5416177f360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc557fbc7cd75a20ee27fd9adebab32041f755214dbc6bffa90cc0225b39da2e5c2d3b8480a28151839015610b595780836020610b5595519101845af4610b4f6114a4565b916117ba565b5080f35b50505034610b645780f35b807fb398979f0000000000000000000000000000000000000000000000000000000060049252fd5b7f4c9c8ce3000000000000000000000000000000000000000000000000000000008452600452602483fd5b7faa1d49a4000000000000000000000000000000000000000000000000000000008552600452602484fd5b9095506020813d602011610c12575b81610bfe602093836112da565b81010312610c0e5751945f610a41565b8480fd5b3d9150610bf1565b6004827fe07c8dba000000000000000000000000000000000000000000000000000000008152fd5b905073ffffffffffffffffffffffffffffffffffffffff7f360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc541614155f6109e3565b503461016e5760407ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261016e578060043573ffffffffffffffffffffffffffffffffffffffff8116809103610d8657610cde6115b3565b73ffffffffffffffffffffffffffffffffffffffff7f00000000000000000000000000000000000000000000000000000000000000001690813b156107375782916044839260405194859384927f205c2878000000000000000000000000000000000000000000000000000000008452600484015260243560248401525af18015610d7b57610d6a5750f35b81610d74916112da565b61016e5780f35b6040513d84823e3d90fd5b50fd5b505f7ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126102555773ffffffffffffffffffffffffffffffffffffffff7f000000000000000000000000000000000000000000000000000000000000000016803b15610255575f602491604051928380927fb760faf900000000000000000000000000000000000000000000000000000000825230600483015234905af18015610e4357610e37575080f35b61001a91505f906112da565b6040513d5f823e3d90fd5b346102555760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126102555760043567ffffffffffffffff811161025557610e9d9036906004016112a9565b610ea56114d3565b5f7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffa183360301905b8281101561001a578060051b8401358281121561025557840180359073ffffffffffffffffffffffffffffffffffffffff82168203610255575f9181610f25610f1a6040869501836113fb565b91905a923691611382565b926020808551950193013591f115610f3f57600101610ecd565b60018303610f4f5761072f611599565b610f57611599565b90610f9a6040519283927f5a15467500000000000000000000000000000000000000000000000000000000845260048401526040602484015260448301906113b8565b0390fd5b346102555760607ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126102555760043567ffffffffffffffff8111610255576101207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc82360301126102555760443573ffffffffffffffffffffffffffffffffffffffff7f00000000000000000000000000000000000000000000000000000000000000001680330361108857506110606020926024359060040161144c565b9080611070575b50604051908152f35b5f80808093335af1506110816114a4565b5082611067565b7ffe34a6d3000000000000000000000000000000000000000000000000000000005f52336004523060245260445260645ffd5b346102555760807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc360112610255576110f2611235565b506110fb611258565b5060643567ffffffffffffffff81116102555761111c90369060040161127b565b505060206040517f150b7a02000000000000000000000000000000000000000000000000000000008152f35b346102555760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261025557600435907fffffffff00000000000000000000000000000000000000000000000000000000821680920361025557817f150b7a02000000000000000000000000000000000000000000000000000000006020931490811561120b575b81156111e1575b5015158152f35b7f01ffc9a700000000000000000000000000000000000000000000000000000000915014836111da565b7f4e2312e000000000000000000000000000000000000000000000000000000000811491506111d3565b6004359073ffffffffffffffffffffffffffffffffffffffff8216820361025557565b6024359073ffffffffffffffffffffffffffffffffffffffff8216820361025557565b9181601f840112156102555782359167ffffffffffffffff8311610255576020838186019501011161025557565b9181601f840112156102555782359167ffffffffffffffff8311610255576020808501948460051b01011161025557565b90601f7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe0910116810190811067ffffffffffffffff82111761131b57604052565b7f4e487b71000000000000000000000000000000000000000000000000000000005f52604160045260245ffd5b67ffffffffffffffff811161131b57601f017fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe01660200190565b92919261138e82611348565b9161139c60405193846112da565b829481845281830111610255578281602093845f960137010152565b907fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe0601f602080948051918291828752018686015e5f8582860101520116010190565b9035907fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe181360301821215610255570180359067ffffffffffffffff82116102555760200191813603831361025557565b9061149561148c73ffffffffffffffffffffffffffffffffffffffff9261148661147f855f5416966101008101906113fb565b3691611382565b90611619565b90929192611653565b160361149f575f90565b600190565b3d156114ce573d906114b582611348565b916114c360405193846112da565b82523d5f602084013e565b606090565b73ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000168033148015611579575b73ffffffffffffffffffffffffffffffffffffffff5f54169015611536575050565b60849250604051917f62018a3100000000000000000000000000000000000000000000000000000000835233600484015230602484015260448301526064820152fd5b5073ffffffffffffffffffffffffffffffffffffffff5f54163314611514565b3d604051906020818301016040528082525f602083013e90565b73ffffffffffffffffffffffffffffffffffffffff5f54168033148015611610575b156115dd5750565b7fcbce04d8000000000000000000000000000000000000000000000000000000005f52336004523060245260445260645ffd5b503033146115d5565b8151919060418303611649576116429250602082015190606060408401519301515f1a9061172b565b9192909190565b50505f9160029190565b60048110156116fe5780611665575050565b60018103611695577ff645eedf000000000000000000000000000000000000000000000000000000005f5260045ffd5b600281036116c957507ffce698f7000000000000000000000000000000000000000000000000000000005f5260045260245ffd5b6003146116d35750565b7fd78bce0c000000000000000000000000000000000000000000000000000000005f5260045260245ffd5b7f4e487b71000000000000000000000000000000000000000000000000000000005f52602160045260245ffd5b91907f7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a084116117af579160209360809260ff5f9560405194855216868401526040830152606082015282805260015afa15610e43575f5173ffffffffffffffffffffffffffffffffffffffff8116156117a557905f905f90565b505f906001905f90565b5050505f9160039190565b906117f757508051156117cf57805190602001fd5b7fd6bda275000000000000000000000000000000000000000000000000000000005f5260045ffd5b8151158061184a575b611808575090565b73ffffffffffffffffffffffffffffffffffffffff907f9996b315000000000000000000000000000000000000000000000000000000005f521660045260245ffd5b50803b1561180056fea26469706673582212205642151deb7ca46832959c36fe0088d6a9e43b284a551b20a7476d8c61fdbed964736f6c634300081c0033f0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a00";
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

    public partial class NotFromEntryPointError : NotFromEntryPointErrorBase { }

    [Error("NotFromEntryPoint")]
    public class NotFromEntryPointErrorBase : IErrorDTO
    {
        [Parameter("address", "msgSender", 1)]
        public virtual string MsgSender { get; set; }
        [Parameter("address", "entity", 2)]
        public virtual string Entity { get; set; }
        [Parameter("address", "entryPoint", 3)]
        public virtual string EntryPoint { get; set; }
    }

    public partial class NotInitializingError : NotInitializingErrorBase { }
    [Error("NotInitializing")]
    public class NotInitializingErrorBase : IErrorDTO
    {
    }

    public partial class NotOwnerError : NotOwnerErrorBase { }

    [Error("NotOwner")]
    public class NotOwnerErrorBase : IErrorDTO
    {
        [Parameter("address", "msgSender", 1)]
        public virtual string MsgSender { get; set; }
        [Parameter("address", "entity", 2)]
        public virtual string Entity { get; set; }
        [Parameter("address", "owner", 3)]
        public virtual string Owner { get; set; }
    }

    public partial class NotOwnerOrEntryPointError : NotOwnerOrEntryPointErrorBase { }

    [Error("NotOwnerOrEntryPoint")]
    public class NotOwnerOrEntryPointErrorBase : IErrorDTO
    {
        [Parameter("address", "msgSender", 1)]
        public virtual string MsgSender { get; set; }
        [Parameter("address", "entity", 2)]
        public virtual string Entity { get; set; }
        [Parameter("address", "entryPoint", 3)]
        public virtual string EntryPoint { get; set; }
        [Parameter("address", "owner", 4)]
        public virtual string Owner { get; set; }
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
