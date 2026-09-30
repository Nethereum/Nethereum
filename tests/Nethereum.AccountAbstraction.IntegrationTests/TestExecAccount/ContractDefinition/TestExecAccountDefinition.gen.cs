using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestExecAccount.ContractDefinition
{


    public partial class TestExecAccountDeployment : TestExecAccountDeploymentBase
    {
        public TestExecAccountDeployment() : base(BYTECODE) { }
        public TestExecAccountDeployment(string byteCode) : base(byteCode) { }
    }

    public class TestExecAccountDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "60c03461014757601f611e0538819003918201601f19168301916001600160401b0383118484101761014b5780849260209460405283398101031261014757516001600160a01b0381168103610147573060805260a0525f516020611de55f395f51905f525460ff8160401c16610138576002600160401b03196001600160401b038216016100e2575b604051611c8590816101608239608051818181610c0f0152610cf2015260a0518181816101fb015281816103b2015281816105a10152818161079101528181611012015281816110f90152818161135501526118e60152f35b6001600160401b0319166001600160401b039081175f516020611de55f395f51905f52556040519081527fc7f505b2f371ae2175ee4913f4499e1f2633a7b5936321eed1cdaeb6115181d290602090a15f610089565b63f92ee8a960e01b5f5260045ffd5b5f80fd5b634e487b7160e01b5f52604160045260245ffdfe608080604052600436101561001c575b50361561001a575f80fd5b005b5f905f3560e01c90816301ffc9a71461147757508063150b7a02146113ea57806319822f7c146112cd57806334fcd5be1461117d5780634a58db19146110b85780634d44560d14610fba5780634f1ef28614610c8757806352d1902d14610bc95780638da5cb5b14610b785780638dd7712f14610838578063ad3cb1cc146107b5578063b0d691fe14610746578063b61d27f6146106ac578063bc197c81146105da578063c399ec8814610528578063c4d66de814610278578063d087d2881461017c5763f23a6e610361000f57346101795760a07ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017957610121611564565b5061012a611587565b5060843567ffffffffffffffff81116101775761014b9036906004016115cb565b505060206040517ff23a6e61000000000000000000000000000000000000000000000000000000008152f35b505b80fd5b503461017957807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017957604051907f35567e1a00000000000000000000000000000000000000000000000000000000825230600483015280602483015260208260448173ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000165afa90811561026c5790610235575b602090604051908152f35b506020813d602011610264575b8161024f6020938361162a565b81010312610260576020905161022a565b5f80fd5b3d9150610242565b604051903d90823e3d90fd5b50346101795760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc360112610179576102b0611564565b7ff0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a00549060ff8260401c16159167ffffffffffffffff811680159081610520575b6001149081610516575b15908161050d575b506104e55790818360017fffffffffffffffffffffffffffffffffffffffffffffffff000000000000000073ffffffffffffffffffffffffffffffffffffffff9516177ff0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a0055610490575b501690817fffffffffffffffffffffffff00000000000000000000000000000000000000008454161783556040519173ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000167f47e55c76e7a6f1fd8996a1da8008c1ea29699cca35e7bcd057f2dec313b6e5de8580a36103fe575080f35b60207fc7f505b2f371ae2175ee4913f4499e1f2633a7b5936321eed1cdaeb6115181d2917fffffffffffffffffffffffffffffffffffffffffffffff00ffffffffffffffff7ff0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a0054167ff0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a005560018152a180f35b7fffffffffffffffffffffffffffffffffffffffffffffff0000000000000000001668010000000000000001177ff0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a00555f61036c565b6004847ff92ee8a9000000000000000000000000000000000000000000000000000000008152fd5b9050155f610302565b303b1591506102fa565b8491506102f0565b503461017957807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017957604051907f70a0823100000000000000000000000000000000000000000000000000000000825230600483015260208260248173ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000165afa90811561026c579061023557602090604051908152f35b50346101795760a07ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017957610612611564565b5061061b611587565b5060443567ffffffffffffffff81116101775761063c9036906004016115f9565b505060643567ffffffffffffffff81116101775761065e9036906004016115f9565b505060843567ffffffffffffffff8111610177576106809036906004016115cb565b505060206040517fbc197c81000000000000000000000000000000000000000000000000000000008152f35b50346101795760607ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017957806106e5611564565b60443567ffffffffffffffff811161074257829161070a61071d9236906004016115cb565b92906107146118cf565b5a9336916116d2565b916020835193019160243591f1156107325780f35b61073a611995565b602081519101fd5b5050fd5b503461017957807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017957602060405173ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000168152f35b503461017957807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017957506108346040516107f660408261162a565b600581527f352e302e300000000000000000000000000000000000000000000000000000006020820152604051918291602083526020830190611726565b0390f35b50346101795760407ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126101795760043567ffffffffffffffff811161017757806004016101207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc8336030112610b74576108b36118cf565b8260648301916108c38382611769565b929083600411610177576060937ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc8101610a72575b827fd3fddfd1276d1cc278f10907710a44474a32f917b2fcfa198f46ca7689215e2f6109c888610a6c89610a5e8d610a2d8c610104610a25604051998a9960408b5273ffffffffffffffffffffffffffffffffffffffff610958866115aa565b1660408c0152602487013560608c01526109986109918c61016061097f60448c018a6117e9565b91909261012060808201520191611839565b91866117e9565b8c83037fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc00160a08e015290611839565b608486013560c08b015260a486013560e08b015260c48601356101008b01526109f460e48701856117e9565b907fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc08c8403016101208d0152611839565b9301906117e9565b907fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc087840301610140880152611839565b908382036020850152611726565b0390a180f35b908092939594500160407ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc8383030112610b7457610ab2600483016115aa565b9060248301359067ffffffffffffffff8211610b70576004610ad79286950101611708565b908273ffffffffffffffffffffffffffffffffffffffff60208451940192165af192610b016117ba565b9315610b1257929091845f806108f8565b60646040517f08c379a000000000000000000000000000000000000000000000000000000000815260206004820152601160248201527f696e6e65722063616c6c206661696c65640000000000000000000000000000006044820152fd5b8480fd5b8280fd5b503461017957807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126101795773ffffffffffffffffffffffffffffffffffffffff6020915416604051908152f35b503461017957807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126101795773ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000163003610c5f5760206040517f360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc8152f35b807fe07c8dba0000000000000000000000000000000000000000000000000000000060049252fd5b5060407ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017957610cba611564565b9060243567ffffffffffffffff811161017757610cdb903690600401611708565b73ffffffffffffffffffffffffffffffffffffffff7f000000000000000000000000000000000000000000000000000000000000000016803014908115610f78575b50610f5057610d2a6119af565b73ffffffffffffffffffffffffffffffffffffffff831690604051937f52d1902d000000000000000000000000000000000000000000000000000000008552602085600481865afa80958596610f1c575b50610dac57602484847f4c9c8ce3000000000000000000000000000000000000000000000000000000008252600452fd5b9091847f360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc8103610ef15750813b15610ec657807fffffffffffffffffffffffff00000000000000000000000000000000000000007f360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc5416177f360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc557fbc7cd75a20ee27fd9adebab32041f755214dbc6bffa90cc0225b39da2e5c2d3b8480a28151839015610e935780836020610e8f95519101845af4610e896117ba565b91611bb6565b5080f35b50505034610e9e5780f35b807fb398979f0000000000000000000000000000000000000000000000000000000060049252fd5b7f4c9c8ce3000000000000000000000000000000000000000000000000000000008452600452602483fd5b7faa1d49a4000000000000000000000000000000000000000000000000000000008552600452602484fd5b9095506020813d602011610f48575b81610f386020938361162a565b81010312610b705751945f610d7b565b3d9150610f2b565b6004827fe07c8dba000000000000000000000000000000000000000000000000000000008152fd5b905073ffffffffffffffffffffffffffffffffffffffff7f360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc541614155f610d1d565b50346101795760407ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126101795780610ff3611564565b610ffb6119af565b73ffffffffffffffffffffffffffffffffffffffff7f00000000000000000000000000000000000000000000000000000000000000001690813b156107425773ffffffffffffffffffffffffffffffffffffffff604484928360405195869485937f205c287800000000000000000000000000000000000000000000000000000000855216600484015260243560248401525af180156110ad5761109c5750f35b816110a69161162a565b6101795780f35b6040513d84823e3d90fd5b505f7ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126102605773ffffffffffffffffffffffffffffffffffffffff7f000000000000000000000000000000000000000000000000000000000000000016803b15610260575f602491604051928380927fb760faf900000000000000000000000000000000000000000000000000000000825230600483015234905af1801561117257611166575080f35b61001a91505f9061162a565b6040513d5f823e3d90fd5b346102605760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126102605760043567ffffffffffffffff8111610260576111cc9036906004016115f9565b6111d46118cf565b5f7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffa183360301905b8281101561001a578060051b8401358281121561026057840180359073ffffffffffffffffffffffffffffffffffffffff82168203610260575f9181611254611249604086950183611769565b91905a9236916116d2565b926020808551950193013591f11561126e576001016111fc565b6001830361127e5761073a611995565b611286611995565b906112c96040519283927f5a1546750000000000000000000000000000000000000000000000000000000084526004840152604060248401526044830190611726565b0390fd5b346102605760607ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126102605760043567ffffffffffffffff8111610260576101207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc82360301126102605760443573ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000168033036113b7575061138f60209260243590600401611877565b908061139f575b50604051908152f35b5f80808093335af1506113b06117ba565b5082611396565b7ffe34a6d3000000000000000000000000000000000000000000000000000000005f52336004523060245260445260645ffd5b346102605760807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261026057611421611564565b5061142a611587565b5060643567ffffffffffffffff81116102605761144b9036906004016115cb565b505060206040517f150b7a02000000000000000000000000000000000000000000000000000000008152f35b346102605760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261026057600435907fffffffff00000000000000000000000000000000000000000000000000000000821680920361026057817f150b7a02000000000000000000000000000000000000000000000000000000006020931490811561153a575b8115611510575b5015158152f35b7f01ffc9a70000000000000000000000000000000000000000000000000000000091501483611509565b7f4e2312e00000000000000000000000000000000000000000000000000000000081149150611502565b6004359073ffffffffffffffffffffffffffffffffffffffff8216820361026057565b6024359073ffffffffffffffffffffffffffffffffffffffff8216820361026057565b359073ffffffffffffffffffffffffffffffffffffffff8216820361026057565b9181601f840112156102605782359167ffffffffffffffff8311610260576020838186019501011161026057565b9181601f840112156102605782359167ffffffffffffffff8311610260576020808501948460051b01011161026057565b90601f7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe0910116810190811067ffffffffffffffff82111761166b57604052565b7f4e487b71000000000000000000000000000000000000000000000000000000005f52604160045260245ffd5b67ffffffffffffffff811161166b57601f017fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe01660200190565b9291926116de82611698565b916116ec604051938461162a565b829481845281830111610260578281602093845f960137010152565b9080601f8301121561026057816020611723933591016116d2565b90565b907fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe0601f602080948051918291828752018686015e5f8582860101520116010190565b9035907fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe181360301821215610260570180359067ffffffffffffffff82116102605760200191813603831361026057565b3d156117e4573d906117cb82611698565b916117d9604051938461162a565b82523d5f602084013e565b606090565b90357fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe18236030181121561026057016020813591019167ffffffffffffffff821161026057813603831361026057565b601f82602094937fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe093818652868601375f8582860101520116010190565b906118c06118b773ffffffffffffffffffffffffffffffffffffffff926118b16118aa855f541696610100810190611769565b36916116d2565b90611a15565b90929192611a4f565b16036118ca575f90565b600190565b73ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000168033148015611975575b73ffffffffffffffffffffffffffffffffffffffff5f54169015611932575050565b60849250604051917f62018a3100000000000000000000000000000000000000000000000000000000835233600484015230602484015260448301526064820152fd5b5073ffffffffffffffffffffffffffffffffffffffff5f54163314611910565b3d604051906020818301016040528082525f602083013e90565b73ffffffffffffffffffffffffffffffffffffffff5f54168033148015611a0c575b156119d95750565b7fcbce04d8000000000000000000000000000000000000000000000000000000005f52336004523060245260445260645ffd5b503033146119d1565b8151919060418303611a4557611a3e9250602082015190606060408401519301515f1a90611b27565b9192909190565b50505f9160029190565b6004811015611afa5780611a61575050565b60018103611a91577ff645eedf000000000000000000000000000000000000000000000000000000005f5260045ffd5b60028103611ac557507ffce698f7000000000000000000000000000000000000000000000000000000005f5260045260245ffd5b600314611acf5750565b7fd78bce0c000000000000000000000000000000000000000000000000000000005f5260045260245ffd5b7f4e487b71000000000000000000000000000000000000000000000000000000005f52602160045260245ffd5b91907f7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a08411611bab579160209360809260ff5f9560405194855216868401526040830152606082015282805260015afa15611172575f5173ffffffffffffffffffffffffffffffffffffffff811615611ba157905f905f90565b505f906001905f90565b5050505f9160039190565b90611bf35750805115611bcb57805190602001fd5b7fd6bda275000000000000000000000000000000000000000000000000000000005f5260045ffd5b81511580611c46575b611c04575090565b73ffffffffffffffffffffffffffffffffffffffff907f9996b315000000000000000000000000000000000000000000000000000000005f521660045260245ffd5b50803b15611bfc56fea26469706673582212200e645762a609f1b221a94f86ff484c0435aad5484aac5b2688e7943d6a885ed164736f6c634300081c0033f0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a00";
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
