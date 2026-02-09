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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator.ContractDefinition
{


    public partial class OwnableValidatorDeployment : OwnableValidatorDeploymentBase
    {
        public OwnableValidatorDeployment() : base(BYTECODE) { }
        public OwnableValidatorDeployment(string byteCode) : base(byteCode) { }
    }

    public class OwnableValidatorDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60808060405234601557611865908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c806306fdde031461010457806354fd4d50146100ff5780636d61fe70146100fa5780637065cb48146100f55780638a91b0e3146100f0578063940d3840146100eb578063960bfe04146100e657806397003203146100e1578063c86ec2bf146100dc578063ccfdec8c146100d7578063d60b347f146100d2578063ecd05961146100cd578063f551e2ee146100c8578063fbe5ce0a146100c35763fd8b84b1146100be575f80fd5b6109b4565b610909565b6108ab565b610873565b610835565b6107fa565b6107bf565b610773565b6106da565b61066b565b610485565b6103b1565b61023f565b61019b565b610141565b805180835260209291819084018484015e5f828201840152601f01601f1916010190565b90602061013e928181520190610109565b90565b34610197575f36600319011261019757610193604051610162604082610a3b565b601081526f27bbb730b13632ab30b634b230ba37b960811b6020820152604051918291602083526020830190610109565b0390f35b5f80fd5b34610197575f366003190112610197576101936040516101bc604082610a3b565b60058152640312e302e360dc1b6020820152604051918291602083526020830190610109565b9181601f840112156101975782359167ffffffffffffffff8311610197576020838186019501011161019757565b6020600319820112610197576004359067ffffffffffffffff82116101975761023b916004016101e2565b9091565b346101975761025861025036610210565b810190610a7a565b610268610264826115ba565b1590565b6103915781156103825780519180831061037357335f908152600160205260409020556020821161036457335f9081526002602052604090209091908190556102b033610dc9565b5f915b8183106102e157337f27b541a16df0902e262f34789782092ab25125513b8ed73608e802951771b9285f80a2005b6102fb6102ee8483610b06565b516001600160a01b031690565b926001600160a01b03841680156103475761031a600193949533610e37565b337fc82bdbbf677a2462f2a7e22e4ba9abd209496b69cd7b868b3b1d28f76e09a40a5f80a30191906102b3565b63b20f76e360e01b5f526001600160a01b03851660045260245b5ffd5b632414149d60e01b5f5260045ffd5b63aabd5a0960e01b5f5260045ffd5b6306968de960e31b5f5260045ffd5b63e719027360e01b5f5260045ffd5b6001600160a01b0381160361019757565b34610197576020366003190112610197576004356103ce816103a0565b335f9081526001602052604090205415610472576001600160a01b03811690811561045757335f90815260026020908152604090912054101561036457335f90815260026020526040902061043091906104288154610b42565b905533610e37565b337fc82bdbbf677a2462f2a7e22e4ba9abd209496b69cd7b868b3b1d28f76e09a40a5f80a3005b63b20f76e360e01b5f526001600160a01b031660045260245ffd5b63f91bd6f160e01b5f523360045260245ffd5b346101975761049336610210565b505061049e33610f62565b506001905f5b8151811015610628576001600160a01b036104bf8284610b06565b51166001600160a01b0381168015801561061e575b61060a575f85815260208181526040808320338452909152902061051090610504905b546001600160a01b031690565b6001600160a01b031690565b036105ef57906001916105936105526104f73361053d855f9060018060a01b03165f5260205260405f2090565b9060018060a01b03165f5260205260405f2090565b6001600160a01b0385165f90815260208190526040902061057490339061053d565b80546001600160a01b0319166001600160a01b03909216919091179055565b6105c46105b43361053d845f9060018060a01b03165f5260205260405f2090565b80546001600160a01b0319169055565b337fe594d081b4382713733fe631966432c9cea5199afb2db5c3c1931f9f930036795f80a3016104a4565b637c84ecfb60e01b5f526001600160a01b031660045260245ffd5b637c84ecfb60e01b5f52600160045260245ffd5b50600181146104d4565b335f9081526001602090815260408083208390556002909152812055337f9d00629762554452d03c3b45626436df6ca1c3795d05d04df882f6db481b1be05f80a2005b346101975760603660031901126101975760043560243567ffffffffffffffff81116101975761069f9036906004016101e2565b91906044359167ffffffffffffffff8311610197576020936106c86106d09436906004016101e2565b939092610b9b565b6040519015158152f35b3461019757602036600319011261019757600435335f908152600160205260409020541561076057801561037357335f9081526002602052604090208190541061037357335f81815260016020908152604091829020849055905192835290917ff7e18aa0532694077d6fc7df02e85d86b91ba964f958d1949d45c5776d36eb6e9190a2005b63f91bd6f160e01b5f523360045260245ffd5b346101975760403660031901126101975760043567ffffffffffffffff8111610197576101206003198236030112610197576107b760209160243590600401610d10565b604051908152f35b34610197576020366003190112610197576004356107dc816103a0565b60018060a01b03165f526001602052602060405f2054604051908152f35b3461019757602036600319011261019757600435610817816103a0565b60018060a01b03165f526002602052602060405f2054604051908152f35b346101975760203660031901126101975760206106d0600435610857816103a0565b6001600160a01b03165f90815260016020526040902054151590565b34610197576020366003190112610197576020600435600181149081156108a0575b506040519015158152f35b60079150145f610895565b34610197576060366003190112610197576108c76004356103a0565b60243560443567ffffffffffffffff8111610197576020916108f06108f69236906004016101e2565b91610d96565b6040516001600160e01b03199091168152f35b3461019757604036600319011261019757600435610926816103a0565b60243590610933826103a0565b335f52600260205260405f2054335f52600160205260405f2054146109a5578161095d913361105f565b335f52600260205260405f206109738154610dbd565b90556001600160a01b0316337fe594d081b4382713733fe631966432c9cea5199afb2db5c3c1931f9f930036795f80a3005b630f368a7560e11b5f5260045ffd5b34610197576020366003190112610197576109d96004356109d4816103a0565b610f62565b506040518091602082016020835281518091526020604084019201905f5b818110610a05575050500390f35b82516001600160a01b03168452859450602093840193909201916001016109f7565b634e487b7160e01b5f52604160045260245ffd5b90601f8019910116810190811067ffffffffffffffff821117610a5d57604052565b610a27565b67ffffffffffffffff8111610a5d5760051b60200190565b91906040838203126101975782359260208101359067ffffffffffffffff821161019757019080601f83011215610197578135610ab681610a62565b92610ac46040519485610a3b565b81845260208085019260051b82010192831161019757602001905b828210610aec5750505090565b602080918335610afb816103a0565b815201910190610adf565b8051821015610b1a5760209160051b010190565b634e487b7160e01b5f52603260045260245ffd5b634e487b7160e01b5f52601160045260245ffd5b5f198114610b505760010190565b610b2e565b92919267ffffffffffffffff8211610a5d5760405191610b7f601f8201601f191660200184610a3b565b829481845281830111610197578281602093845f960137010152565b9392610baa9193810190610a7a565b929093610bb6846115ba565b15610d07578415610d0757610bd18592610bd7943691610b55565b906111d2565b80516002811060208301918060051b84019115610c54575b505050610bfb816117b7565b5f918151915f5b838110610c1c57505050501015610c17575f90565b600190565b610c32610c2c6102ee8385610b06565b846113fa565b50610c40575b600101610c02565b93610c4c600191610b42565b949050610c38565b82969492959391959086875b8981519180601f190192835111610c78575050610c60565b9091509891929394959697981115610cfa575b80519080601f190191825110610ca15750610c8b565b90509790919293949596971115610ccc57610cc291925f8552601f1961166e565b81525f8080610bef565b602091505b8251815184528152910190601f190180821015610cf057602090610cd1565b50505f8080610bef565b5050829394959650610bef565b50505050505f90565b610d49813592610d1f846103a0565b6020527b19457468657265756d205369676e6564204d6573736167653a0a33325f52603c60042090565b9061010081013590601e198136030182121561019757019081359167ffffffffffffffff83116101975760200190823603821361019757610d899361145d565b610d9257600190565b5f90565b90610da292913361145d565b610db2576001600160e01b031990565b630b135d3f60e11b90565b8015610b50575f190190565b6001600160a01b038082165f9081525f5160206118105f395f51905f52602052604090205416610e28576001600160a01b03165f9081525f5160206118105f395f51905f526020526040902080546001600160a01b0319166001179055565b6329e42f3360e11b5f5260045ffd5b6001600160a01b03821680158015610f18575b610f06575f908152602081815260408083206001600160a01b03851684529091529020546001600160a01b0316610eea5760015f908152602052610ee8919061057490610ecc610eaa6104f7835f5160206118105f395f51905f5261053d565b6001600160a01b0385165f90815260208190526040902061057490849061053d565b60015f9081526020525f5160206118105f395f51905f5261053d565b565b631034f46960e21b5f526001600160a01b03821660045260245ffd5b637c84ecfb60e01b5f5260045260245ffd5b5060018114610e4a565b90610f2c82610a62565b610f396040519182610a3b565b8281528092610f4a601f1991610a62565b0190602036910137565b5f19810191908211610b5057565b90610f6d6020610f22565b60015f9081526020819052909290610f956104f7835f5160206118105f395f51905f5261053d565b6001600160a01b0381168015159081611053575b5080611049575b1561100557610ff96104f78461053d84610fe0610fff96610fd1898d610b06565b6001600160a01b039091169052565b6001600160a01b03165f90815260208190526040902090565b91610b42565b90610f95565b9291506001600160a01b0383166001141580611040575b611024578352565b915061103b6102ee61103584610f54565b85610b06565b918352565b5080151561101c565b5060208210610fb0565b6001915014155f610fa9565b91906001600160a01b03821680158015611131575b611110576001600160a01b038281165f90815260208181526040808320888516845290915290205416036110f4579161053d82610fe0610ee8956105748561053d6110d96104f78361053d6105b49c5f9060018060a01b03165f5260205260405f2090565b6001600160a01b039094165f90815260208190526040902090565b637c84ecfb60e01b5f526001600160a01b03821660045260245ffd5b50637c84ecfb60e01b5f9081526001600160a01b0391909116600452602490fd5b5060018114611074565b90604182029180830460411490151715610b5057565b9081604102916041830403610b5057565b60ff6003199116019060ff8211610b5057565b9060208201809211610b5057565b91908201809211610b5057565b9081602091031261019757516001600160e01b0319811681036101975790565b60409061013e939281528160208201520190610109565b6040513d5f823e3d90fd5b929190926111df8261113b565b938051916111ec84610f22565b9583106113eb575f5b848110611203575050505050565b61122281849060410201602081015190606060408201519101515f1a92565b9160ff8116806113895750506001600160a01b03169061124187611151565b811061136a578561125182611175565b1161134b5760208186010190815190876112738361126e84611175565b611183565b11611331575050604051630b135d3f60e11b8152602081806112998589600484016111b0565b0381865afa90811561132c575f916112fe575b506001600160e01b0319166374eca2c160e11b016112dd5750906112d76001925b610fd1838b610b06565b016111f5565b60405163605d348960e01b81529081906112fa906004830161012d565b0390fd5b61131f915060203d8111611325575b6113178183610a3b565b810190611190565b5f6112ac565b503d61130d565b6111c7565b6338a245ff60e11b5f52600452602452604486905260645ffd5b6338a245ff60e11b5f908152600491909152602452604485905260645ffd5b610361906338a245ff60e11b5f52906064916004525f6024525f604452565b926112d792601e60019695115f146113e1576113dc926113d66113d0896020527b19457468657265756d205369676e6564204d6573736167653a0a33325f52603c60042090565b91611162565b9061163c565b6112cd565b6113dc928761163c565b638baa579f60e01b5f5260045ffd5b8051909290916001600160a01b03169060015b838101908160011c91601f199060041b16860151848114868311176114475784111561143c575060010161140d565b93505f19019261140d565b9450509190935081151580925f19010292141691565b6001600160a01b0381165f9081526001602052604090205493909290918415610d0757610bd18592611490943691610b55565b80516002811060208301918060051b84019115611507575b5050506114b4816117b7565b5f918151915f5b8381106114d057505050501015610c17575f90565b6114e66114e06102ee8385610b06565b846115f7565b6114f3575b6001016114bb565b936114ff600191610b42565b9490506114eb565b82969492959391959086875b8981519180601f19019283511161152b575050611513565b90915098919293949596979811156115ad575b80519080601f190191825110611554575061153e565b9050979091929394959697111561157f5761157591925f8552601f1961166e565b81525f80806114a8565b602091505b8251815184528152910190601f1901808210156115a357602090611584565b50505f80806114a8565b50508293949596506114a8565b90600191805160028110156115cd575050565b60208201935060051b015b602083519301928351118184188102156115f257506115d8565b925050565b6001600160a01b03909116600181141591908261161357505090565b5f908152602081815260408083206001600160a01b039485168452909152902054161515919050565b9392919060ff90604051955f52166020526040526060526020604060805f60015afa505f6060523d6060185191604052565b610180828403111561173e57815183830160011c6020600160ff1b0316601f84160180519091908181811015611735575b5085518081831015611722575b5086528252835251928092805b5b602001805186116116ba57935b8301805186106116c7579384808210156116e85781518151835290526116b9565b50509193506020830190808203611711575b505080820361170857505050565b610ee89261166e565b61171b918561166e565b5f806116fa565b9091908084106116ac579291505f6116ac565b9150905f61169f565b91909260208401938451815110156117a9575b505b602084018281116117a2576020848251960101805186811115611799579085915b60208201520180518681111561178c57908591611774565b5060200194909452611753565b50509350611753565b5092505050565b84518151865290525f611751565b805160028110156117c6575050565b602082019060010160051b8201604083015b805180845103611801575b50602001838282146117f557506117d8565b929150500360051c9052565b6020938401908152926117e356feada5013122d395ba3c54772283fb069b10426056ef8ca54750cb9bb552a59e7da264697066735822122025c7ab541a351b20d10ad9693eb46251268f4633f5ca692214db4329687ecbc064736f6c634300081c0033";
        public OwnableValidatorDeploymentBase() : base(BYTECODE) { }
        public OwnableValidatorDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class AddOwnerFunction : AddOwnerFunctionBase { }

    [Function("addOwner")]
    public class AddOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class GetOwnersFunction : GetOwnersFunctionBase { }

    [Function("getOwners", "address[]")]
    public class GetOwnersFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
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
        [Parameter("bytes32", "hash", 2)]
        public virtual byte[] Hash { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
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

    public partial class OwnerCountFunction : OwnerCountFunctionBase { }

    [Function("ownerCount", "uint256")]
    public class OwnerCountFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class RemoveOwnerFunction : RemoveOwnerFunctionBase { }

    [Function("removeOwner")]
    public class RemoveOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "prevOwner", 1)]
        public virtual string PrevOwner { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
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

    public partial class ValidateSignatureWithDataFunction : ValidateSignatureWithDataFunctionBase { }

    [Function("validateSignatureWithData", "bool")]
    public class ValidateSignatureWithDataFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "hash", 1)]
        public virtual byte[] Hash { get; set; }
        [Parameter("bytes", "signature", 2)]
        public virtual byte[] Signature { get; set; }
        [Parameter("bytes", "data", 3)]
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
    }

    public partial class VersionFunction : VersionFunctionBase { }

    [Function("version", "string")]
    public class VersionFunctionBase : FunctionMessage
    {

    }



    public partial class GetOwnersOutputDTO : GetOwnersOutputDTOBase { }

    [FunctionOutput]
    public class GetOwnersOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address[]", "ownersArray", 1)]
        public virtual List<string> OwnersArray { get; set; }
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





    public partial class OwnerCountOutputDTO : OwnerCountOutputDTOBase { }

    [FunctionOutput]
    public class OwnerCountOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }





    public partial class ThresholdOutputDTO : ThresholdOutputDTOBase { }

    [FunctionOutput]
    public class ThresholdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ValidateSignatureWithDataOutputDTO : ValidateSignatureWithDataOutputDTOBase { }

    [FunctionOutput]
    public class ValidateSignatureWithDataOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
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

    public partial class OwnerAddedEventDTO : OwnerAddedEventDTOBase { }

    [Event("OwnerAdded")]
    public class OwnerAddedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "owner", 2, true )]
        public virtual string Owner { get; set; }
    }

    public partial class OwnerRemovedEventDTO : OwnerRemovedEventDTOBase { }

    [Event("OwnerRemoved")]
    public class OwnerRemovedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "owner", 2, true )]
        public virtual string Owner { get; set; }
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

    public partial class CannotRemoveOwnerError : CannotRemoveOwnerErrorBase { }
    [Error("CannotRemoveOwner")]
    public class CannotRemoveOwnerErrorBase : IErrorDTO
    {
    }

    public partial class InvalidOwnerError : InvalidOwnerErrorBase { }

    [Error("InvalidOwner")]
    public class InvalidOwnerErrorBase : IErrorDTO
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
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

    public partial class MaxOwnersReachedError : MaxOwnersReachedErrorBase { }
    [Error("MaxOwnersReached")]
    public class MaxOwnersReachedErrorBase : IErrorDTO
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
