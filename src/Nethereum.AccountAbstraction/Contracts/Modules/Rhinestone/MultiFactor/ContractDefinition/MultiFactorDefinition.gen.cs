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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.MultiFactor.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.MultiFactor.ContractDefinition
{


    public partial class MultiFactorDeployment : MultiFactorDeploymentBase
    {
        public MultiFactorDeployment() : base(BYTECODE) { }
        public MultiFactorDeployment(string byteCode) : base(byteCode) { }
    }

    public class MultiFactorDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60a034607457601f61128238819003918201601f19168301916001600160401b03831184841017607857808492602094604052833981010312607457516001600160a01b03811681036074576080526040516111f5908161008d823960805181818161011c015281816104b101526107730152f35b5f80fd5b634e487b7160e01b5f52604160045260245ffdfe60806040526004361015610011575f80fd5b5f3560e01c806306433b1b146100f457806306fdde03146100ef5780630e13942b146100ea57806332485778146100e55780633382d0d4146100e057806354fd4d50146100db5780636d61fe70146100d657806375e74509146100d15780638a91b0e3146100cc57806397003203146100c7578063d60b347f146100c2578063e5a98603146100bd578063ecd05961146100b85763f551e2ee146100b3575f80fd5b610bdd565b610bbd565b610af8565b610aad565b610a61565b6109aa565b610911565b610691565b61061b565b610422565b61025a565b6101d1565b61016f565b610107565b5f91031261010357565b5f80fd5b34610103575f366003190112610103576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b805180835260209291819084018484015e5f828201840152601f01601f1916010190565b34610103575f366003190112610103576101bc604051610190604082610c4f565b600b81526a26bab63a34a330b1ba37b960a91b602082015260405191829160208352602083019061014b565b0390f35b6001600160a01b0381160361010357565b34610103576020366003190112610103576004356101ee816101c0565b60018060a01b03165f525f602052606060405f20546001600160801b036040519160ff8116835260ff8160081c16602084015260101c166040820152f35b602435906001600160a01b03198216820361010357565b604435906001600160a01b03198216820361010357565b3461010357604036600319011261010357600435610277816101c0565b61027f61022c565b335f9081526020819052604090205460ff16156103e157335f908152602081905260409020805492906103316102c9601086901c6001600160801b03165b6001600160801b031690565b9461031b336102e8876102fd886102e88c5f52600160205260405f2090565b9060018060a01b03165f5260205260405f2090565b906bffffffffffffffffffffffff60a01b165f5260205260405f2090565b9061032582611130565b516103b2575b50610e72565b5460ff8082169160081c16818110610397575050604080516001600160a01b0319909316835260208301939093526001600160a01b03169133917ff0ea25e65adfb4a97935bfd3c5d6bff0e76f1a7648b56aa7483c983263c5a3ba91819081015b0390a3005b637b7a98f160e01b5f5260ff9081166004521660245260445ffd5b6103db906103c59060081c60ff16610c8a565b845461ff00191660089190911b61ff0016178455565b5f61032b565b63f91bd6f160e01b5f523360045260245ffd5b9181601f840112156101035782359167ffffffffffffffff8311610103576020838186019501011161010357565b346101035760603660031901126101035760043561043f816101c0565b61044761022c565b60443567ffffffffffffffff8111610103576104679036906004016103f4565b90811561060c57335f9081526020819052604090205460ff16156103e157335f908152602081905260409020938454916104ae6102bd846001600160801b039060101c1690565b957f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031691823b156101035760405163529562a160e01b81523360048201526001600160a01b038516602482015260016044820152925f90849060649082905afa908115610607577f46b38d4e947fee1a036283e991dd5ea966f787dc1d4c9cf7be8121b1de4d367996610392966105909561058a946105ed575b506105708b6102e88c6102fd8b6102e833955f52600160205260405f2090565b9461057a86611130565b51156105be575b50503691610cb9565b90610ea7565b604080516001600160a01b0319909516855260208501959095526001600160a01b0316933393918291820190565b6105e6916105d19060081c60ff16610ca7565b61ff0082549160081b169061ff001916179055565b5f80610581565b806105fb5f61060193610c4f565b806100f9565b5f610550565b610c9c565b6338290b9f60e11b5f5260045ffd5b34610103575f366003190112610103576101bc60405161063c604082610c4f565b60058152640312e302e360dc1b602082015260405191829160208352602083019061014b565b6020600319820112610103576004359067ffffffffffffffff82116101035761068d916004016103f4565b9091565b346101035761069f36610662565b335f9081526020819052604090205460ff166108fe576106df81806106d96106d36106cd6106ed9688610cff565b90610d29565b60f81c90565b94610d0d565b508035019060208201913590565b60ff8392931680156108ef5781106108d757335f908152602081905260409020906107266102bd83546001600160801b039060101c1690565b9260ff821161060c57825460ff191660ff91909116908117835560405190815290939033907fae26917cf47301680c456bcda5fa7525b27bc8155376278f483726d0ca2aa0b290602090a27f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316935f9390845b8181106107bd57845461ff001916600887901b61ff0016178555005b6107c8818386610d73565b8035906107e9336102e8846102fd816102e88b5f52600160205260405f2090565b893b156101035760405163529562a160e01b81523360048201526001600160a01b038416602482015260016044820152915f836064818e5afa92831561060757610854936108c3575b5061084f602082019261058a6108488585610d9a565b3691610cb9565b610d9a565b90506108ad575b604080516001600160a01b03198316815260208101869052600193926001600160a01b03169133917f46b38d4e947fee1a036283e991dd5ea966f787dc1d4c9cf7be8121b1de4d36799190a3016107a1565b95906108ba600192610dcd565b9690915061085b565b806105fb5f6108d193610c4f565b5f610832565b637b7a98f160e01b5f5260045260ff1660245260445ffd5b63831761d760e01b5f5260045ffd5b634d6b9dd360e01b5f523360045260245ffd5b34610103576060366003190112610103576101bc610997610992600435610937816101c0565b6102e8602435610946816101c0565b6102fd610951610243565b9160018060a01b0385165f525f6020526001600160801b0360405f205460101c165f52600160205260405f209060018060a01b03165f5260205260405f2090565b611130565b5160405190151581529081906020820190565b34610103576109b836610662565b5050335f525f60205260405f20805460016001600160801b038260101c1601916001600160801b038311610a5c5771ffffffffffffffffffffffffffffffffffff19909116601083901b71ffffffffffffffffffffffffffffffff0000161790557fa48343d7a788164e514c8502be5171e4dbebe3529baf5037fc973ddaa97bbb4160405180610a576001600160801b03339516829190602083019252565b0390a2005b610c76565b346101035760403660031901126101035760043567ffffffffffffffff811161010357610120600319823603011261010357610aa560209160243590600401610dde565b604051908152f35b34610103576020366003190112610103576020610aee600435610acf816101c0565b6001600160a01b03165f9081526020819052604090205460ff16151590565b6040519015158152f35b346101035760203660031901126101035760043560ff811680820361010357335f9081526020819052604090205460ff16156103e157335f90815260208190526040902090610b4c825460ff9060081c1690565b8160ff821610610ba15750156108ef57805460ff191660ff831617905560405160ff909116815233907fae26917cf47301680c456bcda5fa7525b27bc8155376278f483726d0ca2aa0b2908060208101610a57565b637b7a98f160e01b5f5260ff908116600452831660245260445ffd5b346101035760203660031901126101035760206040516001600435148152f35b3461010357606036600319011261010357610bf96004356101c0565b60243560443567ffffffffffffffff811161010357602091610c22610c289236906004016103f4565b91610e42565b6040516001600160e01b03199091168152f35b634e487b7160e01b5f52604160045260245ffd5b90601f8019910116810190811067ffffffffffffffff821117610c7157604052565b610c3b565b634e487b7160e01b5f52601160045260245ffd5b60ff5f199116019060ff8211610a5c57565b6040513d5f823e3d90fd5b60ff60019116019060ff8211610a5c57565b92919267ffffffffffffffff8211610c715760405191610ce3601f8201601f191660200184610c4f565b829481845281830111610103578281602093845f960137010152565b906001116101035790600190565b909291928360011161010357831161010357600101915f190190565b356001600160f81b0319811692919060018210610d44575050565b6001600160f81b031960019290920360031b82901b16169150565b634e487b7160e01b5f52603260045260245ffd5b9190811015610d955760051b81013590603e1981360301821215610103570190565b610d5f565b903590601e1981360301821215610103570180359067ffffffffffffffff82116101035760200191813603831361010357565b60ff1660ff8114610a5c5760010190565b90610e3591610df1610100820182610d9a565b50803501903591610e01836101c0565b6020527b19457468657265756d205369676e6564204d6573736167653a0a33325f52603c6004209160208235920190610f8b565b610e3e57600190565b5f90565b90610e57925080350160208135910133610f8b565b610e67576001600160e01b031990565b630b135d3f60e11b90565b5f81555f5b600a8110610e83575050565b805f600180938501015501610e77565b8051821015610d955760209160051b010190565b81516101408111610103578060051c60018101809111610a5c57610eca816110fe565b935f5b828110610f055750505081558151915f5b838110610eeb5750505050565b80610ef860019284610e93565b5182828601015501610ede565b80600180920160051b830151610f1b8289610e93565b5201610ecd565b90816020910312610103575180151581036101035790565b909280608093610f7a9695845260606020850152816060850152848401375f838284010152601f801991011681019060408382840301910152019061014b565b90565b5f198114610a5c5760010190565b909392935f9483156110dd576001600160a01b0383165f9081526020819052604090205492610fc6601085901c6001600160801b03166102bd565b905f955f5b818110610ff957505050505050610fe5610feb9160ff1690565b60ff1690565b1115610ff357565b60019150565b611004818388610d73565b8035611027610992866102e8846102fd816102e88d5f52600160205260405f2090565b918251156110cd579061103f82602080940190610d9a565b9190896110626040519687958694859463025034e160e61b865260048601610f3a565b03916001600160a01b03165afa908115610607575f9161109f575b5061108b575b600101610fcb565b96611097600191610f7d565b979050611083565b6110c0915060203d81116110c6575b6110b88183610c4f565b810190610f22565b5f61107d565b503d6110ae565b505f9b5050505050505050505050565b505f9450505050565b67ffffffffffffffff8111610c715760051b60200190565b90611108826110e6565b6111156040519182610c4f565b8281528092611126601f19916110e6565b0190602036910137565b9081548060051c60018101809111610a5c5761114b816110fe565b935f5b8281106111a5575050506040519283602081019160208151939101925f5b81811061118c575050611188925003601f198101855284610c4f565b8252565b845183526020948501948894509092019160010161116c565b8060018092840101546111b88289610e93565b520161114e56fea2646970667358221220613eda129a0976411e53b33a93320ed9e2549c552827cda7d56f28936642c6da64736f6c634300081c0033";
        public MultiFactorDeploymentBase() : base(BYTECODE) { }
        public MultiFactorDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_registry", 1)]
        public virtual string Registry { get; set; }
    }

    public partial class RegistryFunction : RegistryFunctionBase { }

    [Function("REGISTRY", "address")]
    public class RegistryFunctionBase : FunctionMessage
    {

    }

    public partial class AccountConfigFunction : AccountConfigFunctionBase { }

    [Function("accountConfig", typeof(AccountConfigOutputDTO))]
    public class AccountConfigFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class IsInitializedFunction : IsInitializedFunctionBase { }

    [Function("isInitialized", "bool")]
    public class IsInitializedFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class IsModuleTypeFunction : IsModuleTypeFunctionBase { }

    [Function("isModuleType", "bool")]
    public class IsModuleTypeFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "typeID", 1)]
        public virtual BigInteger TypeID { get; set; }
    }

    public partial class IsSubValidatorFunction : IsSubValidatorFunctionBase { }

    [Function("isSubValidator", "bool")]
    public class IsSubValidatorFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("address", "subValidator", 2)]
        public virtual string SubValidator { get; set; }
        [Parameter("bytes12", "id", 3)]
        public virtual byte[] Id { get; set; }
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

    public partial class RemoveValidatorFunction : RemoveValidatorFunctionBase { }

    [Function("removeValidator")]
    public class RemoveValidatorFunctionBase : FunctionMessage
    {
        [Parameter("address", "validatorAddress", 1)]
        public virtual string ValidatorAddress { get; set; }
        [Parameter("bytes12", "id", 2)]
        public virtual byte[] Id { get; set; }
    }

    public partial class SetThresholdFunction : SetThresholdFunctionBase { }

    [Function("setThreshold")]
    public class SetThresholdFunctionBase : FunctionMessage
    {
        [Parameter("uint8", "threshold", 1)]
        public virtual byte Threshold { get; set; }
    }

    public partial class SetValidatorFunction : SetValidatorFunctionBase { }

    [Function("setValidator")]
    public class SetValidatorFunctionBase : FunctionMessage
    {
        [Parameter("address", "validatorAddress", 1)]
        public virtual string ValidatorAddress { get; set; }
        [Parameter("bytes12", "id", 2)]
        public virtual byte[] Id { get; set; }
        [Parameter("bytes", "newValidatorData", 3)]
        public virtual byte[] NewValidatorData { get; set; }
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

    public partial class RegistryOutputDTO : RegistryOutputDTOBase { }

    [FunctionOutput]
    public class RegistryOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class AccountConfigOutputDTO : AccountConfigOutputDTOBase { }

    [FunctionOutput]
    public class AccountConfigOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint8", "threshold", 1)]
        public virtual byte Threshold { get; set; }
        [Parameter("uint8", "validationLength", 2)]
        public virtual byte ValidationLength { get; set; }
        [Parameter("uint128", "iteration", 3)]
        public virtual BigInteger Iteration { get; set; }
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

    public partial class IsSubValidatorOutputDTO : IsSubValidatorOutputDTOBase { }

    [FunctionOutput]
    public class IsSubValidatorOutputDTOBase : IFunctionOutputDTO 
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













    public partial class VersionOutputDTO : VersionOutputDTOBase { }

    [FunctionOutput]
    public class VersionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class IterationIncreasedEventDTO : IterationIncreasedEventDTOBase { }

    [Event("IterationIncreased")]
    public class IterationIncreasedEventDTOBase : IEventDTO
    {
        [Parameter("address", "smartAccount", 1, true )]
        public virtual string SmartAccount { get; set; }
        [Parameter("uint256", "iteration", 2, false )]
        public virtual BigInteger Iteration { get; set; }
    }

    public partial class ThesholdSetEventDTO : ThesholdSetEventDTOBase { }

    [Event("ThesholdSet")]
    public class ThesholdSetEventDTOBase : IEventDTO
    {
        [Parameter("address", "smartAccount", 1, true )]
        public virtual string SmartAccount { get; set; }
        [Parameter("uint8", "threshold", 2, false )]
        public virtual byte Threshold { get; set; }
    }

    public partial class ValidatorAddedEventDTO : ValidatorAddedEventDTOBase { }

    [Event("ValidatorAdded")]
    public class ValidatorAddedEventDTOBase : IEventDTO
    {
        [Parameter("address", "smartAccount", 1, true )]
        public virtual string SmartAccount { get; set; }
        [Parameter("address", "validator", 2, true )]
        public virtual string Validator { get; set; }
        [Parameter("bytes12", "id", 3, false )]
        public virtual byte[] Id { get; set; }
        [Parameter("uint256", "iteration", 4, false )]
        public virtual BigInteger Iteration { get; set; }
    }

    public partial class ValidatorRemovedEventDTO : ValidatorRemovedEventDTOBase { }

    [Event("ValidatorRemoved")]
    public class ValidatorRemovedEventDTOBase : IEventDTO
    {
        [Parameter("address", "smartAccount", 1, true )]
        public virtual string SmartAccount { get; set; }
        [Parameter("address", "validator", 2, true )]
        public virtual string Validator { get; set; }
        [Parameter("bytes12", "id", 3, false )]
        public virtual byte[] Id { get; set; }
        [Parameter("uint256", "iteration", 4, false )]
        public virtual BigInteger Iteration { get; set; }
    }

    public partial class InvalidThresholdError : InvalidThresholdErrorBase { }

    [Error("InvalidThreshold")]
    public class InvalidThresholdErrorBase : IErrorDTO
    {
        [Parameter("uint256", "length", 1)]
        public virtual BigInteger Length { get; set; }
        [Parameter("uint256", "threshold", 2)]
        public virtual BigInteger Threshold { get; set; }
    }

    public partial class InvalidValidatorDataError : InvalidValidatorDataErrorBase { }
    [Error("InvalidValidatorData")]
    public class InvalidValidatorDataErrorBase : IErrorDTO
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

    public partial class ZeroThresholdError : ZeroThresholdErrorBase { }
    [Error("ZeroThreshold")]
    public class ZeroThresholdErrorBase : IErrorDTO
    {
    }
}
