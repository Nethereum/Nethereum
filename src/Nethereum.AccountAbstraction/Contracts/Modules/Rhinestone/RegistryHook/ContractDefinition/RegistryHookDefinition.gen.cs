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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.RegistryHook.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.RegistryHook.ContractDefinition
{


    public partial class RegistryHookDeployment : RegistryHookDeploymentBase
    {
        public RegistryHookDeployment() : base(BYTECODE) { }
        public RegistryHookDeployment(string byteCode) : base(byteCode) { }
    }

    public class RegistryHookDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60808060405234601557610b7a908161001a8239f35b5f80fdfe6080806040526004361015610012575f80fd5b5f3560e01c908163038defd71461055d5750806306fdde031461050f578063173bf7da146104d457806342a75597146104a257806354fd4d501461045b5780636d61fe701461039f57806376b880241461036057806383e7dbe41461030a5780638a91b0e31461026d578063a91ee0dc146101c8578063d60b347f14610181578063d68f60251461011f578063da742228146100d95763ecd05961146100b6575f80fd5b346100d55760203660031901126100d557602060405160048035148152f35b5f80fd5b346100d55760203660031901126100d5576100f261059c565b335f90815260208190526040902080546001600160a01b0319166001600160a01b03909216919091179055005b346100d55760603660031901126100d55761013861059c565b60443567ffffffffffffffff81116100d55761017d9161015f6101699236906004016105d6565b9160243590610652565b6040519182916020835260208301906105b2565b0390f35b346100d55760203660031901126100d55760206101be61019f61059c565b6001600160a01b039081165f9081526001602052604090205416151590565b6040519015158152f35b346100d55760203660031901126100d5576101e161059c565b335f908152600160205260409020546001600160a01b03161561025a57335f8181526001602090815260409182902080546001600160a01b0319166001600160a01b03959095169485179055905192835290917f363c56730e510c61b9b1c8da206585b5f5fa0eb1f76e05c2fcf82ee006fff9f59190a2005b63f91bd6f160e01b5f523360045260245ffd5b346100d55760203660031901126100d55760043567ffffffffffffffff81116100d55761029e9036906004016105d6565b5050335f52600160205260405f206001600160601b0360a01b81541690556102dc335f525f60205260405f206001600160601b0360a01b8154169055565b6040515f81527f363c56730e510c61b9b1c8da206585b5f5fa0eb1f76e05c2fcf82ee006fff9f560203392a2005b346100d55760403660031901126100d55761032361059c565b6024356001600160a01b03811691908290036100d5576020915f525f825260018060a01b0360405f2054169060018060a01b031614604051908152f35b346100d55760203660031901126100d5576001600160a01b0361038161059c565b165f525f602052602060018060a01b0360405f205416604051908152f35b346100d55760203660031901126100d55760043567ffffffffffffffff81116100d5576103d09036906004016105d6565b335f908152600160205260409020546001600160a01b0316610448576014116100d557335f8181526001602090815260409182902080546001600160a01b031916943560601c9485179055905192835290917f363c56730e510c61b9b1c8da206585b5f5fa0eb1f76e05c2fcf82ee006fff9f59190a2005b634d6b9dd360e01b5f523360045260245ffd5b346100d5575f3660031901126100d55761017d60405161047c604082610604565b60058152640312e302e360dc1b60208201526040519182916020835260208301906105b2565b346100d5575f3660031901126100d5576104d2335f525f60205260405f206001600160601b0360a01b8154169055565b005b346100d55760203660031901126100d55760043567ffffffffffffffff81116100d5576105059036906004016105d6565b50506104d2610707565b346100d5575f3660031901126100d55761017d604051610530604082610604565b600c81526b5265676973747279486f6f6b60a01b60208201526040519182916020835260208301906105b2565b346100d55760203660031901126100d5576020906001600160a01b0361058161059c565b165f9081526001835260409020546001600160a01b03168152f35b600435906001600160a01b03821682036100d557565b805180835260209291819084018484015e5f828201840152601f01601f1916010190565b9181601f840112156100d55782359167ffffffffffffffff83116100d557602083818601950101116100d557565b90601f8019910116810190811067ffffffffffffffff82111761062657604052565b634e487b7160e01b5f52604160045260245ffd5b909392938483116100d55784116100d5578101920390565b9091836004116100d557638dd7712f60e01b81356001600160e01b031916036106fb578360c4116100d55760a4810135604481018082116106d65760648201918282116106d6576106a783606493898761063a565b903590602081106106ea575b500101908181116106d6576106d3956106cb9361063a565b92909161075d565b90565b634e487b7160e01b5f52601160045260245ffd5b5f199060200360031b1b165f6106b3565b90916106d3939261075d565b3390602836101561071457565b60131936013560601c60271936013560601c338114908161073c575b506107385750565b9150565b5f838152602081905260408120546001600160a01b03169091149150610730565b90506060836004116100d55782356001600160e01b03191663e9ae5c5360e01b8103610790575050906106d39291610a35565b6335a4725960e21b81036107ab575050906106d3929161091f565b909150639517e29f60e01b81036108a85750506084821180156108a157826084116100d5576064820135905b1561089b5780608401806084116106d65783106100d557505b816024116100d5576004810135916044116100d557603001356001600160a01b03610819610707565b165f818152600160205260409020549091906001600160a01b0316803b156100d55760405163529562a160e01b8152600481019390935260609190911c60248301526044820192909252905f90829060649082905afa801561089057610880575b50606090565b5f61088a91610604565b5f61087a565b6040513d5f823e3d90fd5b506107f0565b5f906107d7565b6314e2ec7560e31b0361091057506084821190811561090957826084116100d5576064810135915b156109015781608401806084116106d65783106100d55750505b806024116100d5576044116100d55761087a610707565b9050506108ea565b5f916108d0565b91505061091b610707565b5090565b91806064116100d55760448201359182606401806064116106d65782106100d55760648101916024116100d5576004013560ff60f81b1680155f146109e657509061096991610b1c565b506001600160a01b03925061098091506107079050565b165f818152600160205260409020546001600160a01b031691823b156100d55760405163529562a160e01b815260048101929092526001600160a01b0316602482015260026044820152905f90829060649082905afa8015610890576108805750606090565b9250600160f81b8303610a07576109fd9250610a9c565b505061087a610707565b50906001600160f81b031903610a26576014116100d55761087a610707565b6339d2eb5560e01b5f5260045ffd5b50816064116100d55760448101359081606401806064116106d65783106100d55760648101926024116100d5576004013560ff60f81b169182155f14610a8b57610a7f9250610b1c565b5050505061087a610707565b600160f81b8303610a07576109fd92505b909181359182810193601f199101016020840193803593828560051b8301119060401c17610b0f5783610acd575050565b835b5f190160208160051b8301013580830160608101908135809101918680602080860135809601011191111792171760401c17610b0f5780610acf57505050565b63ba597e7e5f526004601cfd5b90806014116100d557813560601c92603482106100d55760148301359260340191603319019056fea2646970667358221220b21a6d669d09e0fe5ed8616000ea62a07637ebb3940ff738f74a177c34154e3364736f6c634300081c0033";
        public RegistryHookDeploymentBase() : base(BYTECODE) { }
        public RegistryHookDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class ClearTrustedForwarderFunction : ClearTrustedForwarderFunctionBase { }

    [Function("clearTrustedForwarder")]
    public class ClearTrustedForwarderFunctionBase : FunctionMessage
    {

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

    public partial class RegistryFunction : RegistryFunctionBase { }

    [Function("registry", "address")]
    public class RegistryFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class SetRegistryFunction : SetRegistryFunctionBase { }

    [Function("setRegistry")]
    public class SetRegistryFunctionBase : FunctionMessage
    {
        [Parameter("address", "_registry", 1)]
        public virtual string Registry { get; set; }
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

    public partial class VersionFunction : VersionFunctionBase { }

    [Function("version", "string")]
    public class VersionFunctionBase : FunctionMessage
    {

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

    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }









    public partial class RegistryOutputDTO : RegistryOutputDTOBase { }

    [FunctionOutput]
    public class RegistryOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
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

    public partial class RegistrySetEventDTO : RegistrySetEventDTOBase { }

    [Event("RegistrySet")]
    public class RegistrySetEventDTOBase : IEventDTO
    {
        [Parameter("address", "smartAccount", 1, true )]
        public virtual string SmartAccount { get; set; }
        [Parameter("address", "registry", 2, false )]
        public virtual string Registry { get; set; }
    }

    public partial class HookInvalidSelectorError : HookInvalidSelectorErrorBase { }
    [Error("HookInvalidSelector")]
    public class HookInvalidSelectorErrorBase : IErrorDTO
    {
    }

    public partial class InvalidCallTypeError : InvalidCallTypeErrorBase { }
    [Error("InvalidCallType")]
    public class InvalidCallTypeErrorBase : IErrorDTO
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
}
