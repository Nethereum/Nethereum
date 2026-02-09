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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery.ContractDefinition
{


    public partial class SocialRecoveryDeployment : SocialRecoveryDeploymentBase
    {
        public SocialRecoveryDeployment() : base(BYTECODE) { }
        public SocialRecoveryDeployment(string byteCode) : base(byteCode) { }
    }

    public class SocialRecoveryDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x608080604052346015576117c6908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c806306fdde03146100f45780635040fb76146100ef57806354fd4d50146100ea5780636d61fe70146100e55780638a91b0e3146100e0578063960bfe04146100db57806397003203146100d65780639b27a90e146100d1578063a526d83b146100cc578063c86ec2bf146100c7578063d60b347f146100c2578063ecd05961146100bd578063f18858ab146100b85763f551e2ee146100b3575f80fd5b610a52565b61092f565b6108cc565b610884565b610849565b61077c565b610598565b61054c565b6104af565b6103ba565b610288565b6101e4565b6101a9565b610131565b805180835260209291819084018484015e5f828201840152601f01601f1916010190565b90602061012e9281815201906100f9565b90565b34610194575f36600319011261019457610190604051610152604082610ab3565b601781527f536f6369616c5265636f7665727956616c696461746f7200000000000000000060208201526040519182916020835260208301906100f9565b0390f35b5f80fd5b6001600160a01b0381160361019457565b34610194576020366003190112610194576004356101c681610198565b60018060a01b03165f526002602052602060405f2054604051908152f35b34610194575f36600319011261019457610190604051610205604082610ab3565b60058152640312e302e360dc1b60208201526040519182916020835260208301906100f9565b9181601f840112156101945782359167ffffffffffffffff8311610194576020838186019501011161019457565b6020600319820112610194576004359067ffffffffffffffff8211610194576102849160040161022b565b9091565b34610194576102a161029936610259565b810190610af2565b6102b16102ad82611484565b1590565b6103ab57811561039c5780519180831061038d576020831161037e57335f908152600260205260409020839055335f908152600160205260409020556102f633610eb7565b5f5b82811061032657337f27b541a16df0902e262f34789782092ab25125513b8ed73608e802951771b9285f80a2005b6103406103338284610b7e565b516001600160a01b031690565b6001600160a01b03811615610362579061035c60019233610f25565b016102f8565b63f38d406d60e01b5f526001600160a01b031660045260245b5ffd5b636fe1a40d60e11b5f5260045ffd5b63aabd5a0960e01b5f5260045ffd5b6306968de960e31b5f5260045ffd5b63e719027360e01b5f5260045ffd5b34610194576103c836610259565b505060015f9081526020526001600160a01b03610403335f5160206117715f395f51905f525b9060018060a01b03165f5260205260405f2090565b54165b6001600160a01b03811661045757335f9081526001602090815260408083208390556002909152812055337f9d00629762554452d03c3b45626436df6ca1c3795d05d04df882f6db481b1be05f80a2005b6001600160a01b038082165f90815260208190526040902061049c919061047f9033906103ee565b5416916103ee339160018060a01b03165f525f60205260405f2090565b80546001600160a01b0319169055610406565b3461019457602036600319011261019457600435335f908152600160205260409020541561053957801561038d57335f9081526002602052604090208190541061038d57335f90815260016020526040902081905560405190815233907ff7e18aa0532694077d6fc7df02e85d86b91ba964f958d1949d45c5776d36eb6e9080602081015b0390a2005b63f91bd6f160e01b5f523360045260245ffd5b346101945760403660031901126101945760043567ffffffffffffffff81116101945761012060031982360301126101945761059060209160243590600401610cdf565b604051908152f35b34610194576040366003190112610194576004356105b581610198565b602435906105c282610198565b335f52600260205260405f2054335f52600160205260405f20541461076d576001600160a01b03821680158015610763575b6107425761063461062861061b336103ee8660018060a01b03165f525f60205260405f2090565b546001600160a01b031690565b6001600160a01b031690565b036107265761069e9061067f61066061061b336103ee8760018060a01b03165f525f60205260405f2090565b6001600160a01b039092165f90815260208190526040902033906103ee565b80546001600160a01b0319166001600160a01b03909216919091179055565b6106ce6106be336103ee8460018060a01b03165f525f60205260405f2090565b80546001600160a01b0319169055565b335f9081526002602052604090206106e68154610eab565b90556040516001600160a01b03909116815233907fee943cdb81826d5909c559c6b1ae6908fcaf2dbc16c4b730346736b486283e8b908060208101610534565b637c84ecfb60e01b5f526001600160a01b03821660045260245ffd5b50637c84ecfb60e01b5f9081526001600160a01b0391909116600452602490fd5b50600181146105f4565b63380da21160e11b5f5260045ffd5b346101945760203660031901126101945760043561079981610198565b335f9081526001602052604090205415610836576001600160a01b0381161561036257335f90815260026020908152604090912054101561037e57335f9081526002602052604090206107ec8154610c3d565b90556107f88133610f25565b6040516001600160a01b03909116815233907fbc3292102fa77e083913064b282926717cdfaede4d35f553d66366c0a3da755a908060208101610534565b63f91bd6f160e01b5f523360045260245ffd5b346101945760203660031901126101945760043561086681610198565b60018060a01b03165f526001602052602060405f2054604051908152f35b346101945760203660031901126101945760206108c26004356108a681610198565b6001600160a01b03165f90815260016020526040902054151590565b6040519015158152f35b34610194576020366003190112610194576020600435600160405191148152f35b60206040818301928281528451809452019201905f5b8181106109105750505090565b82516001600160a01b0316845260209384019390920191600101610903565b346101945760203660031901126101945760043561094c81610198565b610954611037565b60015f90815260208190529161097a61061b825f5160206117715f395f51905f526103ee565b6001600160a01b0381168015159081610a46575b5080610a3c575b156109ea576109de61061b836103ee846109c56109e4966109b68b8b610b7e565b6001600160a01b039091169052565b6001600160a01b03165f90815260208190526040902090565b93610c3d565b9261097a565b61019090839085906001600160a01b03166001141580610a33575b610a18575b8152604051918291826108ed565b610a2d610333610a2783611476565b84610b7e565b50610a0a565b50801515610a05565b5060208410610995565b6001915014155f61098e565b3461019457606036600319011261019457610a6e600435610198565b60443567ffffffffffffffff811161019457610a8e90369060040161022b565b5050639ba6061b60e01b5f5260045ffd5b634e487b7160e01b5f52604160045260245ffd5b90601f8019910116810190811067ffffffffffffffff821117610ad557604052565b610a9f565b67ffffffffffffffff8111610ad55760051b60200190565b91906040838203126101945782359260208101359067ffffffffffffffff821161019457019080601f83011215610194578135610b2e81610ada565b92610b3c6040519485610ab3565b81845260208085019260051b82010192831161019457602001905b828210610b645750505090565b602080918335610b7381610198565b815201910190610b57565b8051821015610b925760209160051b010190565b634e487b7160e01b5f52603260045260245ffd5b3561012e81610198565b903590601e1981360301821215610194570180359067ffffffffffffffff82116101945760200191813603831361019457565b92919267ffffffffffffffff8211610ad55760405191610c0d601f8201601f191660200184610ab3565b829481845281830111610194578281602093845f960137010152565b634e487b7160e01b5f52601160045260245ffd5b5f198114610c4b5760010190565b610c29565b906004116101945790600490565b906008116101945760040190600490565b909291928360041161019457831161019457600401916003190190565b909291928360641161019457831161019457606401916063190190565b356001600160e01b0319811692919060048210610cc4575050565b6001600160e01b031960049290920360031b82901b16169150565b610ce881610ba6565b6001600160a01b0381165f90815260016020526040902090939054928315610ea15783610d3c610d5a926020527b19457468657265756d205369676e6564204d6573736167653a0a33325f52603c60042090565b610d54610d4d610100870187610bb0565b3691610be3565b906110ff565b90610d648261163c565b610d6d826116f0565b5f925f5b8351811015610db157610d90610d8a6103338387610b7e565b88611327565b610d9d575b600101610d71565b93610da9600191610c3d565b949050610d95565b50939150935f936060810190610de3610dd3610dcd8484610bb0565b90610c50565b6001600160e01b03199291610ca9565b1663e9ae5c5360e01b8103610e2b5750610e0a9394955090610e0491610bb0565b916113a0565b915b10159081610e23575b50610e1f57600190565b5f90565b90505f610e15565b939493638dd7712f60e01b14610e44575b505050610e0c565b610e6c610e79610e5d610e578585610bb0565b90610c5e565b63e9ae5c5360e01b9391610ca9565b6001600160e01b03191690565b03610e3c57610e98939550610e0491610e9191610bb0565b8091610c6f565b915f8080610e3c565b5092505050600190565b8015610c4b575f190190565b6001600160a01b038082165f9081525f5160206117715f395f51905f52602052604090205416610f16576001600160a01b03165f9081525f5160206117715f395f51905f526020526040902080546001600160a01b0319166001179055565b6329e42f3360e11b5f5260045ffd5b6001600160a01b03821680158015611006575b610ff4575f908152602081815260408083206001600160a01b03851684529091529020546001600160a01b0316610fd85760015f908152602052610fd6919061067f90610fba610f9861061b835f5160206117715f395f51905f526103ee565b6001600160a01b0385165f90815260208190526040902061067f9084906103ee565b60015f9081526020525f5160206117715f395f51905f526103ee565b565b631034f46960e21b5f526001600160a01b03821660045260245ffd5b637c84ecfb60e01b5f5260045260245ffd5b5060018114610f38565b90604182029180830460411490151715610c4b57565b9081604102916041830403610c4b57565b60405161042091906110498382610ab3565b60208152918290601f190190369060200137565b9061106782610ada565b6110746040519182610ab3565b8281528092611085601f1991610ada565b0190602036910137565b60ff6003199116019060ff8211610c4b57565b9060208201809211610c4b57565b91908201809211610c4b57565b9081602091031261019457516001600160e01b0319811681036101945790565b60409061012e9392815281602082015201906100f9565b6040513d5f823e3d90fd5b9291909261110c82611010565b938051916111198461105d565b958310611318575f5b848110611130575050505050565b61114f81849060410201602081015190606060408201519101515f1a92565b9160ff8116806112b65750506001600160a01b03169061116e87611026565b8110611297578561117e826110a2565b116112785760208186010190815190876111a08361119b846110a2565b6110b0565b1161125e575050604051630b135d3f60e11b8152602081806111c68589600484016110dd565b0381865afa908115611259575f9161122b575b506001600160e01b0319166374eca2c160e11b0161120a5750906112046001925b6109b6838b610b7e565b01611122565b60405163605d348960e01b8152908190611227906004830161011d565b0390fd5b61124c915060203d8111611252575b6112448183610ab3565b8101906110bd565b5f6111d9565b503d61123a565b6110f4565b6338a245ff60e11b5f52600452602452604486905260645ffd5b6338a245ff60e11b5f908152600491909152602452604485905260645ffd5b61037b906338a245ff60e11b5f52906064916004525f6024525f604452565b9261120492601e60019695115f1461130e57611309926113036112fd896020527b19457468657265756d205369676e6564204d6573736167653a0a33325f52603c60042090565b9161108f565b906114c1565b6111fa565b61130992876114c1565b638baa579f60e01b5f5260045ffd5b6001600160a01b03909116600181141591908261134357505090565b5f908152602081815260408083206001600160a01b039485168452909152902054161515919050565b35906020811061137a575090565b5f199060200360031b1b1690565b90816020910312610194575180151581036101945790565b82602411610194576113c76113b960206004850161136c565b6001600160f81b0319161590565b1561146f576113e56113df8461142895602095610c8c565b90611748565b505060405163112d3a7d60e01b8152600160048201526001600160a01b03909216602483015250606060448201525f606482015292839190829081906084820190565b03916001600160a01b03165afa908115611259575f91611446575090565b61012e915060203d602011611468575b6114608183610ab3565b810190611388565b503d611456565b5050505f90565b5f19810191908211610c4b57565b9060019180516002811015611497575050565b60208201935060051b015b602083519301928351118184188102156114bc57506114a2565b925050565b9392919060ff90604051955f52166020526040526060526020604060805f60015afa505f6060523d6060185191604052565b61018082840311156115c357815183830160011c6020600160ff1b0316601f841601805190919081818110156115ba575b50855180818310156115a7575b5086528252835251928092805b5b6020018051861161153f57935b83018051861061154c5793848082101561156d57815181518352905261153e565b50509193506020830190808203611596575b505080820361158d57505050565b610fd6926114f3565b6115a091856114f3565b5f8061157f565b909190808410611531579291505f611531565b9150905f611524565b919092602084019384518151101561162e575b505b6020840182811161162757602084825196010180518681111561161e579085915b602082015201805186811115611611579085916115f9565b50602001949094526115d8565b505093506115d8565b5092505050565b84518151865290525f6115d6565b80519060028210918060051b921561165357505050565b60208201939282019291835b8581519180601f19019283511161167757505061165f565b909150949192939411156116e857805b80519080601f19019182511061169d5750611687565b869293949596915011156116be575f83526116bb9190601f196114f3565b52565b9250602091505b8251815184528152910190601f1901808210156116e4576020906116c5565b5050565b509192505050565b805160028110156116ff575050565b602082019060010160051b8201604083015b80518084510361173a575b506020018382821461172e5750611711565b929150500360051c9052565b60209384019081529261171c565b908060141161019457813560601c92816034116101945760148301359260340191603319019056feada5013122d395ba3c54772283fb069b10426056ef8ca54750cb9bb552a59e7da264697066735822122053bd414f12fd05eba148d67de58d4a4f689ff388427f2d77314444af948e6cc164736f6c634300081c0033";
        public SocialRecoveryDeploymentBase() : base(BYTECODE) { }
        public SocialRecoveryDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class AddGuardianFunction : AddGuardianFunctionBase { }

    [Function("addGuardian")]
    public class AddGuardianFunctionBase : FunctionMessage
    {
        [Parameter("address", "guardian", 1)]
        public virtual string Guardian { get; set; }
    }

    public partial class GetGuardiansFunction : GetGuardiansFunctionBase { }

    [Function("getGuardians", "address[]")]
    public class GetGuardiansFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class GuardianCountFunction : GuardianCountFunctionBase { }

    [Function("guardianCount", "uint256")]
    public class GuardianCountFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class IsInitializedFunction : IsInitializedFunctionBase { }

    [Function("isInitialized", "bool")]
    public class IsInitializedFunctionBase : FunctionMessage
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
    }

    public partial class IsModuleTypeFunction : IsModuleTypeFunctionBase { }

    [Function("isModuleType", "bool")]
    public class IsModuleTypeFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "typeID", 1)]
        public virtual BigInteger TypeID { get; set; }
    }

    public partial class IsValidSignatureWithSenderFunction : IsValidSignatureWithSenderFunctionBase { }

    [Function("isValidSignatureWithSender", "bytes4")]
    public class IsValidSignatureWithSenderFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("bytes32", "", 2)]
        public virtual byte[] ReturnValue2 { get; set; }
        [Parameter("bytes", "", 3)]
        public virtual byte[] ReturnValue3 { get; set; }
    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {

    }

    public partial class OnInstallFunction : OnInstallFunctionBase { }

    [Function("onInstall")]
    public class OnInstallFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "data", 1)]
        public virtual byte[] Data { get; set; }
    }

    public partial class OnUninstallFunction : OnUninstallFunctionBase { }

    [Function("onUninstall")]
    public class OnUninstallFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class RemoveGuardianFunction : RemoveGuardianFunctionBase { }

    [Function("removeGuardian")]
    public class RemoveGuardianFunctionBase : FunctionMessage
    {
        [Parameter("address", "prevGuardian", 1)]
        public virtual string PrevGuardian { get; set; }
        [Parameter("address", "guardian", 2)]
        public virtual string Guardian { get; set; }
    }

    public partial class SetThresholdFunction : SetThresholdFunctionBase { }

    [Function("setThreshold")]
    public class SetThresholdFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_threshold", 1)]
        public virtual BigInteger Threshold { get; set; }
    }

    public partial class ThresholdFunction : ThresholdFunctionBase { }

    [Function("threshold", "uint256")]
    public class ThresholdFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class ValidateUserOpFunction : ValidateUserOpFunctionBase { }

    [Function("validateUserOp", "uint256")]
    public class ValidateUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
    }

    public partial class VersionFunction : VersionFunctionBase { }

    [Function("version", "string")]
    public class VersionFunctionBase : FunctionMessage
    {

    }



    public partial class GetGuardiansOutputDTO : GetGuardiansOutputDTOBase { }

    [FunctionOutput]
    public class GetGuardiansOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address[]", "guardiansArray", 1)]
        public virtual List<string> GuardiansArray { get; set; }
    }

    public partial class GuardianCountOutputDTO : GuardianCountOutputDTOBase { }

    [FunctionOutput]
    public class GuardianCountOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class IsInitializedOutputDTO : IsInitializedOutputDTOBase { }

    [FunctionOutput]
    public class IsInitializedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsModuleTypeOutputDTO : IsModuleTypeOutputDTOBase { }

    [FunctionOutput]
    public class IsModuleTypeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsValidSignatureWithSenderOutputDTO : IsValidSignatureWithSenderOutputDTOBase { }

    [FunctionOutput]
    public class IsValidSignatureWithSenderOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes4", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }









    public partial class ThresholdOutputDTO : ThresholdOutputDTOBase { }

    [FunctionOutput]
    public class ThresholdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ValidateUserOpOutputDTO : ValidateUserOpOutputDTOBase { }

    [FunctionOutput]
    public class ValidateUserOpOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class VersionOutputDTO : VersionOutputDTOBase { }

    [FunctionOutput]
    public class VersionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GuardianAddedEventDTO : GuardianAddedEventDTOBase { }

    [Event("GuardianAdded")]
    public class GuardianAddedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "guardian", 2, false )]
        public virtual string Guardian { get; set; }
    }

    public partial class GuardianRemovedEventDTO : GuardianRemovedEventDTOBase { }

    [Event("GuardianRemoved")]
    public class GuardianRemovedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "guardian", 2, false )]
        public virtual string Guardian { get; set; }
    }

    public partial class ModuleInitializedEventDTO : ModuleInitializedEventDTOBase { }

    [Event("ModuleInitialized")]
    public class ModuleInitializedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
    }

    public partial class ModuleUninitializedEventDTO : ModuleUninitializedEventDTOBase { }

    [Event("ModuleUninitialized")]
    public class ModuleUninitializedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
    }

    public partial class ThresholdSetEventDTO : ThresholdSetEventDTOBase { }

    [Event("ThresholdSet")]
    public class ThresholdSetEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("uint256", "threshold", 2, false )]
        public virtual BigInteger Threshold { get; set; }
    }

    public partial class CannotRemoveGuardianError : CannotRemoveGuardianErrorBase { }
    [Error("CannotRemoveGuardian")]
    public class CannotRemoveGuardianErrorBase : IErrorDTO
    {
    }

    public partial class InvalidGuardianError : InvalidGuardianErrorBase { }

    [Error("InvalidGuardian")]
    public class InvalidGuardianErrorBase : IErrorDTO
    {
        [Parameter("address", "guardian", 1)]
        public virtual string Guardian { get; set; }
    }

    public partial class InvalidSignatureError : InvalidSignatureErrorBase { }
    [Error("InvalidSignature")]
    public class InvalidSignatureErrorBase : IErrorDTO
    {
    }

    public partial class InvalidThresholdError : InvalidThresholdErrorBase { }
    [Error("InvalidThreshold")]
    public class InvalidThresholdErrorBase : IErrorDTO
    {
    }

    public partial class LinkedlistAlreadyinitializedError : LinkedlistAlreadyinitializedErrorBase { }
    [Error("LinkedList_AlreadyInitialized")]
    public class LinkedlistAlreadyinitializedErrorBase : IErrorDTO
    {
    }

    public partial class LinkedlistEntryalreadyinlistError : LinkedlistEntryalreadyinlistErrorBase { }

    [Error("LinkedList_EntryAlreadyInList")]
    public class LinkedlistEntryalreadyinlistErrorBase : IErrorDTO
    {
        [Parameter("address", "entry", 1)]
        public virtual string Entry { get; set; }
    }

    public partial class LinkedlistInvalidentryError : LinkedlistInvalidentryErrorBase { }

    [Error("LinkedList_InvalidEntry")]
    public class LinkedlistInvalidentryErrorBase : IErrorDTO
    {
        [Parameter("address", "entry", 1)]
        public virtual string Entry { get; set; }
    }

    public partial class LinkedlistInvalidpageError : LinkedlistInvalidpageErrorBase { }
    [Error("LinkedList_InvalidPage")]
    public class LinkedlistInvalidpageErrorBase : IErrorDTO
    {
    }

    public partial class MaxGuardiansReachedError : MaxGuardiansReachedErrorBase { }
    [Error("MaxGuardiansReached")]
    public class MaxGuardiansReachedErrorBase : IErrorDTO
    {
    }

    public partial class ModuleAlreadyInitializedError : ModuleAlreadyInitializedErrorBase { }

    [Error("ModuleAlreadyInitialized")]
    public class ModuleAlreadyInitializedErrorBase : IErrorDTO
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
    }

    public partial class NotInitializedError : NotInitializedErrorBase { }

    [Error("NotInitialized")]
    public class NotInitializedErrorBase : IErrorDTO
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
    }

    public partial class NotSortedAndUniqueError : NotSortedAndUniqueErrorBase { }
    [Error("NotSortedAndUnique")]
    public class NotSortedAndUniqueErrorBase : IErrorDTO
    {
    }

    public partial class ThresholdNotSetError : ThresholdNotSetErrorBase { }
    [Error("ThresholdNotSet")]
    public class ThresholdNotSetErrorBase : IErrorDTO
    {
    }

    public partial class UnsupportedOperationError : UnsupportedOperationErrorBase { }
    [Error("UnsupportedOperation")]
    public class UnsupportedOperationErrorBase : IErrorDTO
    {
    }

    public partial class WrongContractSignatureError : WrongContractSignatureErrorBase { }

    [Error("WrongContractSignature")]
    public class WrongContractSignatureErrorBase : IErrorDTO
    {
        [Parameter("bytes", "contractSignature", 1)]
        public virtual byte[] ContractSignature { get; set; }
    }

    public partial class WrongContractSignatureFormatError : WrongContractSignatureFormatErrorBase { }

    [Error("WrongContractSignatureFormat")]
    public class WrongContractSignatureFormatErrorBase : IErrorDTO
    {
        [Parameter("uint256", "s", 1)]
        public virtual BigInteger S { get; set; }
        [Parameter("uint256", "contractSignatureLen", 2)]
        public virtual BigInteger ContractSignatureLen { get; set; }
        [Parameter("uint256", "signaturesLen", 3)]
        public virtual BigInteger SignaturesLen { get; set; }
    }
}
