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
using Nethereum.AccountAbstraction.Contracts.Core.ModularSmartAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Core.ModularSmartAccount.ContractDefinition
{


    public partial class ModularSmartAccountDeployment : ModularSmartAccountDeploymentBase
    {
        public ModularSmartAccountDeployment() : base(BYTECODE) { }
        public ModularSmartAccountDeployment(string byteCode) : base(byteCode) { }
    }

    public class ModularSmartAccountDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60e03461016157601f611bbc38819003918201601f19168301916001600160401b0383118484101761016557808492604094855283398101031261016157610052602061004b83610179565b9201610179565b903060805260a0525f516020611b9c5f395f51905f525460ff8160401c16610152576002600160401b03196001600160401b038216016100fc575b506001600160a01b031660c052604051611a0e908161018e823960805181818161077301526108a4015260a0518181816101af015281816105750152818161061801528181610aea01528181610bd30152818161106a01526111a5015260c051818181610c82015261132d0152f35b6001600160401b0319166001600160401b039081175f516020611b9c5f395f51905f52556040519081527fc7f505b2f371ae2175ee4913f4499e1f2633a7b5936321eed1cdaeb6115181d290602090a15f61008d565b63f92ee8a960e01b5f5260045ffd5b5f80fd5b634e487b7160e01b5f52604160045260245ffd5b51906001600160a01b03821682036101615756fe6080604052600436101561001a575b3615610018575f80fd5b005b5f3560e01c806319822f7c14610174578063392e53cd1461016f5780633e4293961461016a5780634592efab14610165578063460dddab1461016057806347e1da2a1461015b5780634a58db19146101565780634d44560d146101515780634f1ef2861461014c57806352d1902d1461014757806357d562671461014257806362c1729f1461013d57806385acd641146101295780638da5cb5b14610138578063a6f9dae114610133578063ad3cb1cc1461012e578063b0b6cc1a14610129578063b0d691fe14610124578063b61d27f61461011f578063c399ec881461011a578063c4f228b4146101155763d089e11a0361000e57610c6d565b610c3e565b610ba8565b610b2d565b610ad5565b610987565b610a8e565b6109e0565b6109b9565b610937565b6108e9565b610892565b610720565b6105e2565b610567565b6104da565b610423565b61039b565b6102cd565b610272565b34610264576060366003190112610264576004356001600160401b038111610264576101206003198236030112610264576001600160a01b037f000000000000000000000000000000000000000000000000000000000000000016906044359060243533849003610255576101eb9160040161132a565b9181610202575b60405183815280602081015b0390f35b5f80809381935af161021261151e565b501561021f575f806101f2565b60405162461bcd60e51b815260206004820152600e60248201526d141c99599d5b990819985a5b195960921b6044820152606490fd5b63bd07c55160e01b5f5260045ffd5b5f80fd5b5f91031261026457565b34610264575f36600319011261026457602060ff600454166040519015158152f35b60206040818301928281528451809452019201905f5b8181106102b75750505090565b82518452602093840193909201916001016102aa565b34610264575f3660031901126102645760405180602060025491828152019060025f527f405787fa12a823e0f2b7631cc41b3ba8828b3321ca811111fa75cd3aa3bb5ace905f5b818110610337576101fe8561032b818703826106a9565b60405191829182610294565b8254845260209093019260019283019201610314565b6001600160a01b0381160361026457565b35906103698261034d565b565b9181601f84011215610264578235916001600160401b038311610264576020808501948460051b01011161026457565b34610264576080366003190112610264576004356103b88161034d565b6024356001600160401b038111610264576103d790369060040161036b565b916044356001600160401b038111610264576103f790369060040161036b565b90606435946001600160401b0386116102645761041b61001896369060040161036b565b959094610da9565b34610264576020366003190112610264576004355f526001602052602060018060a01b0360405f2054161515604051908152f35b805180835260209291819084018484015e5f828201840152601f01601f1916010190565b602081016020825282518091526040820191602060408360051b8301019401925f915b8383106104ad57505050505090565b90919293946020806104cb600193603f198682030187528951610457565b9701930193019193929061049e565b34610264576060366003190112610264576004356001600160401b0381116102645761050a90369060040161036b565b6024356001600160401b0381116102645761052990369060040161036b565b919092604435926001600160401b038411610264576101fe9461055361055b95369060040161036b565b949093610f1e565b6040519182918261047b565b5f5f600319360112610264577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316803b15610264575f6024916040519283809263b760faf960e01b825230600483015234905af180156105dd576105d1575080f35b61001891505f906106a9565b61115a565b34610264575f6040366003190112610264576004356106008161034d565b5f5460243591906001600160a01b03163303610686577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031691823b156102645760405163040b850f60e31b81526001600160a01b0390921660048301526024820152905f908290604490829084905af180156105dd576105d1575080f35b635fc483c560e01b5f5260045ffd5b634e487b7160e01b5f52604160045260245ffd5b90601f801991011681019081106001600160401b038211176106ca57604052565b610695565b6001600160401b0381116106ca57601f01601f191660200190565b9291926106f6826106cf565b9161070460405193846106a9565b829481845281830111610264578281602093845f960137010152565b6040366003190112610264576004356107388161034d565b6024356001600160401b0381116102645736602382011215610264576107689036906024816004013591016106ea565b906001600160a01b037f000000000000000000000000000000000000000000000000000000000000000016308114908115610870575b50610861575f546001600160a01b03163303610686576040516352d1902d60e01b8152916020836004816001600160a01b0386165afa5f9381610830575b506107fd57634c9c8ce360e01b5f526001600160a01b03821660045260245ffd5b905f5160206119b95f395f51905f52830361081c576100189250611734565b632a87526960e21b5f52600483905260245ffd5b61085391945060203d60201161085a575b61084b81836106a9565b8101906111d4565b925f6107dc565b503d610841565b63703e46dd60e11b5f5260045ffd5b5f5160206119b95f395f51905f52546001600160a01b0316141590505f61079e565b34610264575f366003190112610264577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031630036108615760206040515f5160206119b95f395f51905f528152f35b34610264575f366003190112610264576020600254604051908152f35b634e487b7160e01b5f52603260045260245ffd5b6002548110156109325760025f5260205f2001905f90565b610906565b34610264576020366003190112610264576004356002548110156102645760209060025f527f405787fa12a823e0f2b7631cc41b3ba8828b3321ca811111fa75cd3aa3bb5ace0154604051908152f35b34610264576020366003190112610264576004355f526001602052602060018060a01b0360405f205416604051908152f35b34610264575f366003190112610264575f546040516001600160a01b039091168152602090f35b34610264576020366003190112610264576004356109fd8161034d565b5f54906001600160a01b0382169033829003610686576001600160a01b0316918215610a59576001600160a01b03191682175f9081557fb532073b38c83145e3e5135377a08bf9aab55bc0fd7c1179cd4fb995d2a5159c9080a3005b60405162461bcd60e51b815260206004820152600d60248201526c24b73b30b634b21037bbb732b960991b6044820152606490fd5b34610264575f366003190112610264576101fe604051610aaf6040826106a9565b60058152640352e302e360dc1b6020820152604051918291602083526020830190610457565b34610264575f366003190112610264576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b906020610b2a928181520190610457565b90565b3461026457606036600319011261026457600435610b4a8161034d565b60243590604435906001600160401b0382116102645736602383011215610264578160040135906001600160401b038211610264573660248385010111610264576101fe936024610b9c940191611165565b60405191829182610b19565b34610264575f366003190112610264576040516370a0823160e01b81523060048201526020816024817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa80156105dd576101fe915f91610c1f575b506040519081529081906020820190565b610c38915060203d60201161085a5761084b81836106a9565b5f610c0e565b34610264576020366003190112610264576004355f526003602052602060ff60405f2054166040519015158152f35b34610264575f366003190112610264576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b91908110156109325760051b0190565b35610b2a8161034d565b600254680100000000000000008110156106ca57600181016002556002548110156109325760025f527f405787fa12a823e0f2b7631cc41b3ba8828b3321ca811111fa75cd3aa3bb5ace0155565b8015150361026457565b35610b2a81610d19565b6040808252810183905292939290916001600160fb1b03811161026457608092849160051b8091606085013782019160608301906020606082860301910152520191905f5b818110610d7f5750505090565b9091926020806001928635610d938161034d565b848060a01b031681520194019101919091610d72565b6004549497969295919460ff16610f1057878614801590610f06575b610ef757610dff90610ddf600160ff196004541617600455565b60018060a01b03166bffffffffffffffffffffffff60a01b5f5416175f55565b5f5b858110610e43575050507faa160f6d6a3ff0609f742c765d605e1ae7e00434aa865b632fc20511411906be9394610e3e9160405194859485610d2d565b0390a1565b610e4e818787610cb1565b35908115610ee857610ee2600192610ea3610e72610e6d858e8b610cb1565b610cc1565b610e84835f52600160205260405f2090565b80546001600160a01b0319166001600160a01b03909216919091179055565b610eac81610ccb565b610ed1610ec2610ebd858989610cb1565b610d23565b915f52600360205260405f2090565b9060ff801983541691151516179055565b01610e01565b634632571560e01b5f5260045ffd5b63512509d360e11b5f5260045ffd5b5082861415610dc5565b62dc149f60e41b5f5260045ffd5b939294909460018060a01b035f541633141580611066575b611057578086148061104e575b1561101757610f51866110b0565b955f5b818110610f65575050505050505090565b80610f99610f79610e6d600194868c610cb1565b610f8483878a610cb1565b3590610f9184898c61112b565b929091611571565b610fca610faa610e6d83868c610cb1565b610fb583878a610cb1565b3590610fc284898c61112b565b9290916116c3565b610fd4828b611146565b52610fdf818a611146565b50611011610ff1610e6d83868c610cb1565b610ffc83878a610cb1565b359061100984898c61112b565b929091611621565b01610f54565b60405162461bcd60e51b815260206004820152600f60248201526e098cadccee8d040dad2e6dac2e8c6d608b1b6044820152606490fd5b50818114610f43565b63c5ed84e560e01b5f5260045ffd5b50337f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03161415610f36565b6001600160401b0381116106ca5760051b60200190565b906110ba82611099565b6110c760405191826106a9565b82815280926110d8601f1991611099565b01905f5b8281106110e857505050565b8060606020809385010152016110dc565b903590601e198136030182121561026457018035906001600160401b0382116102645760200191813603831361026457565b90821015610932576111429160051b8101906110f9565b9091565b80518210156109325760209160051b010190565b6040513d5f823e3d90fd5b92909160018060a01b035f5416331415806111a1575b611057578261118f8383610b2a9688611571565b61119b838383886116c3565b94611621565b50337f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316141561117b565b90816020910312610264575190565b908160209103126102645751610b2a81610d19565b9035601e19823603018112156102645701602081359101916001600160401b03821161026457813603831361026457565b908060209392818452848401375f828201840152601f01601f1916010190565b92919061132560209160408652611273604087016112668361035e565b6001600160a01b03169052565b8281013560608701526113126113066112c76112a861129560408601866111f8565b61012060808d01526101608c0191611229565b6112b560608601866111f8565b8b8303603f190160a08d015290611229565b608084013560c08a015260a084013560e08a015260c08401356101008a01526112f360e08501856111f8565b8a8303603f19016101208c015290611229565b916101008101906111f8565b878303603f190161014089015290611229565b930152565b907f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316806114a6575b5061137d6113776113706101008501856110f9565b36916106ea565b826116f4565b5f546001600160a01b03166001600160a01b03909116146114a0575f5b600254811015611498576113ba6113b08261091a565b90549060031b1c90565b6113dd6113d96113d2835f52600360205260405f2090565b5460ff1690565b1590565b61148f575f9081526001602052604090206001600160a01b0390611409905b546001600160a01b031690565b16801561148f5760206040518092639700320360e01b8252815f81611432898b60048401611249565b03925af15f918161146f575b5061144f575b506001905b0161139a565b80158015611464575b15611444579250505090565b506001811615611458565b61148891925060203d811161085a5761084b81836106a9565b905f61143e565b50600190611449565b505050600190565b50505f90565b604051639f8a13d760e01b815230600482015290602090829060249082905afa9081156105dd575f916114ef575b50156114e0575f61135b565b631355c51960e21b5f5260045ffd5b611511915060203d602011611517575b61150981836106a9565b8101906111e3565b5f6114d4565b503d6114ff565b3d15611548573d9061152f826106cf565b9161153d60405193846106a9565b82523d5f602084013e565b606090565b610b2a949260609260018060a01b0316825260208201528160408201520191611229565b909392915f5b6002548110156116195761158a8161091a565b90549060031b1c6115ad6113fc60018060a01b03925f52600160205260405f2090565b1690811561161057813b1561026457845f846001948a83896115e560405197889687958694631ad9683160e31b86526004860161154d565b03925af16115f6575b505b01611577565b806116045f61160a936106a9565b80610268565b5f6115ee565b600191506115f0565b505050509050565b909392915f5b6002548110156116195761163a8161091a565b90549060031b1c61165d6113fc60018060a01b03925f52600160205260405f2090565b169081156116ba57813b1561026457845f846001948a8389611695604051978896879586946337fbbe7d60e01b86526004860161154d565b03925af16116a6575b505b01611627565b806116045f6116b4936106a9565b5f61169e565b600191506116a0565b90925f938493826040519384928337810185815203925af16116e361151e565b90156116ec5790565b602081519101fd5b610b2a9161172b917f19457468657265756d205369676e6564204d6573736167653a0a3332000000005f52601c52603c5f20611849565b909291926118a1565b90813b156117b5575f5160206119b95f395f51905f5280546001600160a01b0319166001600160a01b0384169081179091557fbc7cd75a20ee27fd9adebab32041f755214dbc6bffa90cc0225b39da2e5c2d3b5f80a280511561179d5761179a916117d6565b50565b5050346117a657565b63b398979f60e01b5f5260045ffd5b50634c9c8ce360e01b5f9081526001600160a01b0391909116600452602490fd5b905f8091602081519101845af48080611836575b156117f9575050610b2a61191d565b1561181e57639996b31560e01b5f9081526001600160a01b0391909116600452602490fd5b3d15155f0361115a5763d6bda27560e01b5f5260045ffd5b503d1515806117ea5750813b15156117ea565b8151919060418303611879576118729250602082015190606060408401519301515f1a90611936565b9192909190565b50505f9160029190565b6004111561188d57565b634e487b7160e01b5f52602160045260245ffd5b6118aa81611883565b806118b3575050565b6118bc81611883565b600181036118d35763f645eedf60e01b5f5260045ffd5b6118dc81611883565b600281036118f7575063fce698f760e01b5f5260045260245ffd5b80611903600392611883565b1461190b5750565b6335e2f38360e21b5f5260045260245ffd5b604051903d82523d5f602084013e60203d830101604052565b91907f7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a084116119ad579160209360809260ff5f9560405194855216868401526040830152606082015282805260015afa156105dd575f516001600160a01b038116156119a357905f905f90565b505f906001905f90565b5050505f916003919056fe360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbca2646970667358221220ebdba3178b02ef7a76ceaa1a2742e70d3c52a2b07bb6ca62fecb99008f20412264736f6c634300081c0033f0c57e16840df040f15088dc2f81fe391c3923bec73e23a9662efc9c229c6a00";
        public ModularSmartAccountDeploymentBase() : base(BYTECODE) { }
        public ModularSmartAccountDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_entryPoint", 1)]
        public virtual string EntryPoint { get; set; }
        [Parameter("address", "_accountRegistry", 2)]
        public virtual string AccountRegistry { get; set; }
    }

    public partial class UpgradeInterfaceVersionFunction : UpgradeInterfaceVersionFunctionBase { }

    [Function("UPGRADE_INTERFACE_VERSION", "string")]
    public class UpgradeInterfaceVersionFunctionBase : FunctionMessage
    {

    }

    public partial class AccountRegistryFunction : AccountRegistryFunctionBase { }

    [Function("accountRegistry", "address")]
    public class AccountRegistryFunctionBase : FunctionMessage
    {

    }

    public partial class AddDepositFunction : AddDepositFunctionBase { }

    [Function("addDeposit")]
    public class AddDepositFunctionBase : FunctionMessage
    {

    }

    public partial class ChangeOwnerFunction : ChangeOwnerFunctionBase { }

    [Function("changeOwner")]
    public class ChangeOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class EntryPointFunction : EntryPointFunctionBase { }

    [Function("entryPoint", "address")]
    public class EntryPointFunctionBase : FunctionMessage
    {

    }

    public partial class ExecuteFunction : ExecuteFunctionBase { }

    [Function("execute", "bytes")]
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

    [Function("executeBatch", "bytes[]")]
    public class ExecuteBatchFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "targets", 1)]
        public virtual List<string> Targets { get; set; }
        [Parameter("uint256[]", "values", 2)]
        public virtual List<BigInteger> Values { get; set; }
        [Parameter("bytes[]", "datas", 3)]
        public virtual List<byte[]> Datas { get; set; }
    }

    public partial class GetDepositFunction : GetDepositFunctionBase { }

    [Function("getDeposit", "uint256")]
    public class GetDepositFunctionBase : FunctionMessage
    {

    }

    public partial class GetInstalledModulesFunction : GetInstalledModulesFunctionBase { }

    [Function("getInstalledModules", "bytes32[]")]
    public class GetInstalledModulesFunctionBase : FunctionMessage
    {

    }

    public partial class GetModuleFunction : GetModuleFunctionBase { }

    [Function("getModule", "address")]
    public class GetModuleFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "moduleId", 1)]
        public virtual byte[] ModuleId { get; set; }
    }

    public partial class GetModuleCountFunction : GetModuleCountFunctionBase { }

    [Function("getModuleCount", "uint256")]
    public class GetModuleCountFunctionBase : FunctionMessage
    {

    }

    public partial class HasModuleFunction : HasModuleFunctionBase { }

    [Function("hasModule", "bool")]
    public class HasModuleFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "moduleId", 1)]
        public virtual byte[] ModuleId { get; set; }
    }

    public partial class InitializeFunction : InitializeFunctionBase { }

    [Function("initialize")]
    public class InitializeFunctionBase : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("bytes32[]", "moduleIds", 2)]
        public virtual List<byte[]> ModuleIds { get; set; }
        [Parameter("address[]", "moduleAddresses", 3)]
        public virtual List<string> ModuleAddresses { get; set; }
        [Parameter("bool[]", "canValidate", 4)]
        public virtual List<bool> CanValidate { get; set; }
    }

    public partial class InstalledModuleIdsFunction : InstalledModuleIdsFunctionBase { }

    [Function("installedModuleIds", "bytes32")]
    public class InstalledModuleIdsFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class IsInitializedFunction : IsInitializedFunctionBase { }

    [Function("isInitialized", "bool")]
    public class IsInitializedFunctionBase : FunctionMessage
    {

    }

    public partial class ModuleCanValidateFunction : ModuleCanValidateFunctionBase { }

    [Function("moduleCanValidate", "bool")]
    public class ModuleCanValidateFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class ModulesFunction : ModulesFunctionBase { }

    [Function("modules", "address")]
    public class ModulesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
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

    public partial class UpgradeInterfaceVersionOutputDTO : UpgradeInterfaceVersionOutputDTOBase { }

    [FunctionOutput]
    public class UpgradeInterfaceVersionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class AccountRegistryOutputDTO : AccountRegistryOutputDTOBase { }

    [FunctionOutput]
    public class AccountRegistryOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
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

    public partial class GetInstalledModulesOutputDTO : GetInstalledModulesOutputDTOBase { }

    [FunctionOutput]
    public class GetInstalledModulesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32[]", "", 1)]
        public virtual List<byte[]> ReturnValue1 { get; set; }
    }

    public partial class GetModuleOutputDTO : GetModuleOutputDTOBase { }

    [FunctionOutput]
    public class GetModuleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetModuleCountOutputDTO : GetModuleCountOutputDTOBase { }

    [FunctionOutput]
    public class GetModuleCountOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class HasModuleOutputDTO : HasModuleOutputDTOBase { }

    [FunctionOutput]
    public class HasModuleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }



    public partial class InstalledModuleIdsOutputDTO : InstalledModuleIdsOutputDTOBase { }

    [FunctionOutput]
    public class InstalledModuleIdsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class IsInitializedOutputDTO : IsInitializedOutputDTOBase { }

    [FunctionOutput]
    public class IsInitializedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class ModuleCanValidateOutputDTO : ModuleCanValidateOutputDTOBase { }

    [FunctionOutput]
    public class ModuleCanValidateOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class ModulesOutputDTO : ModulesOutputDTOBase { }

    [FunctionOutput]
    public class ModulesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
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







    public partial class InitializedEventDTO : InitializedEventDTOBase { }

    [Event("Initialized")]
    public class InitializedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "version", 1, false )]
        public virtual ulong Version { get; set; }
    }

    public partial class ModulesInstalledEventDTO : ModulesInstalledEventDTOBase { }

    [Event("ModulesInstalled")]
    public class ModulesInstalledEventDTOBase : IEventDTO
    {
        [Parameter("bytes32[]", "moduleIds", 1, false )]
        public virtual List<byte[]> ModuleIds { get; set; }
        [Parameter("address[]", "moduleAddresses", 2, false )]
        public virtual List<string> ModuleAddresses { get; set; }
    }

    public partial class OwnerChangedEventDTO : OwnerChangedEventDTOBase { }

    [Event("OwnerChanged")]
    public class OwnerChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "oldOwner", 1, true )]
        public virtual string OldOwner { get; set; }
        [Parameter("address", "newOwner", 2, true )]
        public virtual string NewOwner { get; set; }
    }

    public partial class UpgradedEventDTO : UpgradedEventDTOBase { }

    [Event("Upgraded")]
    public class UpgradedEventDTOBase : IEventDTO
    {
        [Parameter("address", "implementation", 1, true )]
        public virtual string Implementation { get; set; }
    }

    public partial class AccountNotActiveError : AccountNotActiveErrorBase { }
    [Error("AccountNotActive")]
    public class AccountNotActiveErrorBase : IErrorDTO
    {
    }

    public partial class AddressEmptyCodeError : AddressEmptyCodeErrorBase { }

    [Error("AddressEmptyCode")]
    public class AddressEmptyCodeErrorBase : IErrorDTO
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
    }

    public partial class AlreadyInitializedError : AlreadyInitializedErrorBase { }
    [Error("AlreadyInitialized")]
    public class AlreadyInitializedErrorBase : IErrorDTO
    {
    }

    public partial class ArrayLengthMismatchError : ArrayLengthMismatchErrorBase { }
    [Error("ArrayLengthMismatch")]
    public class ArrayLengthMismatchErrorBase : IErrorDTO
    {
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

    public partial class ExecutionFailedError : ExecutionFailedErrorBase { }
    [Error("ExecutionFailed")]
    public class ExecutionFailedErrorBase : IErrorDTO
    {
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

    public partial class InvalidModuleIdError : InvalidModuleIdErrorBase { }
    [Error("InvalidModuleId")]
    public class InvalidModuleIdErrorBase : IErrorDTO
    {
    }

    public partial class ModuleNotInstalledError : ModuleNotInstalledErrorBase { }
    [Error("ModuleNotInstalled")]
    public class ModuleNotInstalledErrorBase : IErrorDTO
    {
    }

    public partial class NotInitializingError : NotInitializingErrorBase { }
    [Error("NotInitializing")]
    public class NotInitializingErrorBase : IErrorDTO
    {
    }

    public partial class OnlyEntryPointError : OnlyEntryPointErrorBase { }
    [Error("OnlyEntryPoint")]
    public class OnlyEntryPointErrorBase : IErrorDTO
    {
    }

    public partial class OnlyOwnerError : OnlyOwnerErrorBase { }
    [Error("OnlyOwner")]
    public class OnlyOwnerErrorBase : IErrorDTO
    {
    }

    public partial class OnlyOwnerOrEntryPointError : OnlyOwnerOrEntryPointErrorBase { }
    [Error("OnlyOwnerOrEntryPoint")]
    public class OnlyOwnerOrEntryPointErrorBase : IErrorDTO
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
}
