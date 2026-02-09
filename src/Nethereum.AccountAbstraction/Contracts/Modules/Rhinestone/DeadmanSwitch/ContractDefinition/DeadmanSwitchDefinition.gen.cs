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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.DeadmanSwitch.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.DeadmanSwitch.ContractDefinition
{


    public partial class DeadmanSwitchDeployment : DeadmanSwitchDeploymentBase
    {
        public DeadmanSwitchDeployment() : base(BYTECODE) { }
        public DeadmanSwitchDeployment(string byteCode) : base(byteCode) { }
    }

    public class DeadmanSwitchDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60808060405234601557610bab908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c806306fdde03146106465780630e68ec95146105ed578063173bf7da146105b257806342a755971461058257806354fd4d501461053f57806368ce403c146104a55780636d61fe701461046957806376b880241461042a57806383e7dbe4146103d45780638a91b0e31461034c57806396cf07c41461029b578063970032031461024f578063d60b347f14610208578063d68f6025146101a1578063da7422281461015b578063ecd05961146101235763f551e2ee146100d4575f80fd5b3461011f57606036600319011261011f576100ed6106b1565b5060443567ffffffffffffffff811161011f5761010e9036906004016106c7565b5050639ba6061b60e01b5f5260045ffd5b5f80fd5b3461011f57602036600319011261011f57602060043560048114908115610150575b506040519015158152f35b600191501482610145565b3461011f57602036600319011261011f576101746106b1565b335f90815260208190526040902080546001600160a01b0319166001600160a01b03909216919091179055005b3461011f57606036600319011261011f576101ba6106b1565b5060443567ffffffffffffffff811161011f576101db9036906004016106c7565b50506102046101f06101eb6109cc565b610b0f565b60405191829160208352602083019061068d565b0390f35b3461011f57602036600319011261011f5760206102456102266106b1565b6001600160a01b03165f9081526001602052604090205460601c151590565b6040519015158152f35b3461011f57604036600319011261011f5760043567ffffffffffffffff811161011f57610120600319823603011261011f5761029360209160243590600401610864565b604051908152f35b3461011f57602036600319011261011f5760043565ffffffffffff81169081810361011f57335f9081526001602052604090205460601c156103395761030b90335f52600160205260405f209065ffffffffffff60301b82549160301b169065ffffffffffff60301b1916179055565b6040519081527fa43d0941cb81f2864380d879aeb2629d5e6a8f700b8aeb472cdc5570b760788f60203392a2005b63f91bd6f160e01b5f523360045260245ffd5b3461011f57602036600319011261011f5760043567ffffffffffffffff811161011f5761037d9036906004016106c7565b5050335f5260016020525f60408120556103ad335f525f60205260405f206001600160601b0360a01b8154169055565b337f9d00629762554452d03c3b45626436df6ca1c3795d05d04df882f6db481b1be05f80a2005b3461011f57604036600319011261011f576103ed6106b1565b6024356001600160a01b038116919082900361011f576020915f525f825260018060a01b0360405f2054169060018060a01b031614604051908152f35b3461011f57602036600319011261011f576001600160a01b0361044b6106b1565b165f525f602052602060018060a01b0360405f205416604051908152f35b3461011f57602036600319011261011f5760043567ffffffffffffffff811161011f5761049d6104a39136906004016106c7565b9061074f565b005b3461011f57602036600319011261011f576104be6106b1565b335f9081526001602052604090205460601c1561033957335f90815260016020526040902080546001600160601b0316606083901b6bffffffffffffffffffffffff19161790556040516001600160a01b03909116815233907f90e45a4d176746f69a6301a295d11d03e71df22dc54ee547c3a34cc6a405693190602090a2005b3461011f575f36600319011261011f5761020461055c6040610729565b60058152640312e302e360dc1b602082015260405191829160208352602083019061068d565b3461011f575f36600319011261011f576104a3335f525f60205260405f206001600160601b0360a01b8154169055565b3461011f57602036600319011261011f5760043567ffffffffffffffff811161011f576105e39036906004016106c7565b50506104a36109cc565b3461011f57602036600319011261011f576001600160a01b0361060e6106b1565b165f526001602052606060405f20546040519065ffffffffffff8116825265ffffffffffff8160301c166020830152821c6040820152f35b3461011f575f36600319011261011f576102046106636040610729565b600d81526c088cac2c8dac2dca6eed2e8c6d609b1b60208201526040519182916020835260208301905b805180835260209291819084018484015e5f828201840152601f01601f1916010190565b600435906001600160a01b038216820361011f57565b9181601f8401121561011f5782359167ffffffffffffffff831161011f576020838186019501011161011f57565b604051906060820182811067ffffffffffffffff82111761071557604052565b634e487b7160e01b5f52604160045260245ffd5b6040519190601f01601f1916820167ffffffffffffffff81118382101761071557604052565b335f9081526001602052604090205460601c61084a578160141161011f57803560601c91601a1161011f576014013560d01c61081761078c6106f5565b65ffffffffffff42168152602081018381526107f165ffffffffffff6040840192878452335f526001602052818060405f209651161682198654161785555116839065ffffffffffff60301b82549160301b169065ffffffffffff60301b1916179055565b5181546001600160601b031660609190911b6bffffffffffffffffffffffff1916179055565b60405191825260208201527fa28cedbad7a15fe48fea165a34f79a92b17e27ae6cb06032f70b359b8922442a60403392a2565b5061085157565b634d6b9dd360e01b5f523360045260245ffd5b335f52600160205260405f20916108796106f5565b92549165ffffffffffff83168452602084019265ffffffffffff8160301c16845260601c9182604086015282156109c2576020527b19457468657265756d205369676e6564204d6573736167653a0a33325f52603c6004209061010081013590601e198136030182121561011f57019081359167ffffffffffffffff831161011f576020810191833603831361011f5761091c601f8501601f1916602001610729565b91848352602085369201011161011f5765ffffffffffff945f602086889761094b978388013785010152610a22565b935116915116019065ffffffffffff82116109ae57335f52600160205260405f2065ffffffffffff60301b198154169055155f146109a85760015b60ff1660d09190911b6001600160d01b0319161765ffffffffffff60a01b1790565b5f610986565b634e487b7160e01b5f52601160045260245ffd5b5050505050600190565b339060283610156109d957565b60131936013560601c60271936013560601c3381149081610a01575b506109fd5750565b9150565b5f838152602081905260408120546001600160a01b031690911491506109f5565b90915f91906001600160a01b03821615610b075760405192600484019460248501956044860192853b15610a8b57509186939160209593630b135d3f60e11b8852526040845281518501809260045afa9360443d01915afa9151630b135d3f60e11b1491161690565b979650509050815180604014610ae257604114610aa85750505050565b60209293955060608201515f1a835260408201516060525b5f5201516040526020600160805f825afa511860601b3d11915f606052604052565b506020929395506040820151601b8160ff1c01845260018060ff1b0316606052610ac0565b505050505f90565b6001600160a01b0381165f908152600160205260409020546060929190831c15610b62576001600160a01b03165f908152600160205260409020805465ffffffffffff19164265ffffffffffff16179055565b509050610b6f6020610729565b5f81529056fea2646970667358221220c638cd3a364c424499051584966ae71557bc7ebce14d08c48c7219560941f36564736f6c634300081c0033";
        public DeadmanSwitchDeploymentBase() : base(BYTECODE) { }
        public DeadmanSwitchDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class ClearTrustedForwarderFunction : ClearTrustedForwarderFunctionBase { }

    [Function("clearTrustedForwarder")]
    public class ClearTrustedForwarderFunctionBase : FunctionMessage
    {

    }

    public partial class ConfigFunction : ConfigFunctionBase { }

    [Function("config", typeof(ConfigOutputDTO))]
    public class ConfigFunctionBase : FunctionMessage
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

    public partial class IsTrustedForwarderFunction : IsTrustedForwarderFunctionBase { }

    [Function("isTrustedForwarder", "bool")]
    public class IsTrustedForwarderFunctionBase : FunctionMessage
    {
        [Parameter("address", "forwarder", 1)]
        public virtual string Forwarder { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
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

    public partial class PostCheckFunction : PostCheckFunctionBase { }

    [Function("postCheck")]
    public class PostCheckFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "hookData", 1)]
        public virtual byte[] HookData { get; set; }
    }

    public partial class PreCheckFunction : PreCheckFunctionBase { }

    [Function("preCheck", "bytes")]
    public class PreCheckFunctionBase : FunctionMessage
    {
        [Parameter("address", "msgSender", 1)]
        public virtual string MsgSender { get; set; }
        [Parameter("uint256", "msgValue", 2)]
        public virtual BigInteger MsgValue { get; set; }
        [Parameter("bytes", "msgData", 3)]
        public virtual byte[] MsgData { get; set; }
    }

    public partial class SetNomineeFunction : SetNomineeFunctionBase { }

    [Function("setNominee")]
    public class SetNomineeFunctionBase : FunctionMessage
    {
        [Parameter("address", "nominee", 1)]
        public virtual string Nominee { get; set; }
    }

    public partial class SetTimeoutFunction : SetTimeoutFunctionBase { }

    [Function("setTimeout")]
    public class SetTimeoutFunctionBase : FunctionMessage
    {
        [Parameter("uint48", "timeout", 1)]
        public virtual ulong Timeout { get; set; }
    }

    public partial class SetTrustedForwarderFunction : SetTrustedForwarderFunctionBase { }

    [Function("setTrustedForwarder")]
    public class SetTrustedForwarderFunctionBase : FunctionMessage
    {
        [Parameter("address", "forwarder", 1)]
        public virtual string Forwarder { get; set; }
    }

    public partial class TrustedForwarderFunction : TrustedForwarderFunctionBase { }

    [Function("trustedForwarder", "address")]
    public class TrustedForwarderFunctionBase : FunctionMessage
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



    public partial class ConfigOutputDTO : ConfigOutputDTOBase { }

    [FunctionOutput]
    public class ConfigOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint48", "lastAccess", 1)]
        public virtual ulong LastAccess { get; set; }
        [Parameter("uint48", "timeout", 2)]
        public virtual ulong Timeout { get; set; }
        [Parameter("address", "nominee", 3)]
        public virtual string Nominee { get; set; }
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

    public partial class IsTrustedForwarderOutputDTO : IsTrustedForwarderOutputDTOBase { }

    [FunctionOutput]
    public class IsTrustedForwarderOutputDTOBase : IFunctionOutputDTO 
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















    public partial class TrustedForwarderOutputDTO : TrustedForwarderOutputDTOBase { }

    [FunctionOutput]
    public class TrustedForwarderOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "trustedForwarder", 1)]
        public virtual string TrustedForwarder { get; set; }
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
        [Parameter("address", "nominee", 2, false )]
        public virtual string Nominee { get; set; }
        [Parameter("uint48", "timeout", 3, false )]
        public virtual ulong Timeout { get; set; }
    }

    public partial class ModuleUninitializedEventDTO : ModuleUninitializedEventDTOBase { }

    [Event("ModuleUninitialized")]
    public class ModuleUninitializedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
    }

    public partial class NomineeSetEventDTO : NomineeSetEventDTOBase { }

    [Event("NomineeSet")]
    public class NomineeSetEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "nominee", 2, false )]
        public virtual string Nominee { get; set; }
    }

    public partial class TimeoutSetEventDTO : TimeoutSetEventDTOBase { }

    [Event("TimeoutSet")]
    public class TimeoutSetEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("uint48", "timeout", 2, false )]
        public virtual ulong Timeout { get; set; }
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

    public partial class UnsupportedOperationError : UnsupportedOperationErrorBase { }
    [Error("UnsupportedOperation")]
    public class UnsupportedOperationErrorBase : IErrorDTO
    {
    }
}
