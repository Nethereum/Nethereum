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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ISmartAccountFactoryGovernance.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ISmartAccountFactoryGovernance.ContractDefinition
{


    public partial class ISmartAccountFactoryGovernanceDeployment : ISmartAccountFactoryGovernanceDeploymentBase
    {
        public ISmartAccountFactoryGovernanceDeployment() : base(BYTECODE) { }
        public ISmartAccountFactoryGovernanceDeployment(string byteCode) : base(byteCode) { }
    }

    public class ISmartAccountFactoryGovernanceDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public ISmartAccountFactoryGovernanceDeploymentBase() : base(BYTECODE) { }
        public ISmartAccountFactoryGovernanceDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class MaxAdminsFunction : MaxAdminsFunctionBase { }

    [Function("MAX_ADMINS", "uint256")]
    public class MaxAdminsFunctionBase : FunctionMessage
    {

    }

    public partial class AccountRegistryFunction : AccountRegistryFunctionBase { }

    [Function("accountRegistry", "address")]
    public class AccountRegistryFunctionBase : FunctionMessage
    {

    }

    public partial class CreateAccountFunction : CreateAccountFunctionBase { }

    [Function("createAccount", "address")]
    public class CreateAccountFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("bytes32", "salt", 2)]
        public virtual byte[] Salt { get; set; }
        [Parameter("bytes32[]", "moduleIds", 3)]
        public virtual List<byte[]> ModuleIds { get; set; }
    }

    public partial class CreateAccountIfNeededFunction : CreateAccountIfNeededFunctionBase { }

    [Function("createAccountIfNeeded", "address")]
    public class CreateAccountIfNeededFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("bytes32", "salt", 2)]
        public virtual byte[] Salt { get; set; }
        [Parameter("bytes32[]", "moduleIds", 3)]
        public virtual List<byte[]> ModuleIds { get; set; }
    }

    public partial class EntryPointFunction : EntryPointFunctionBase { }

    [Function("entryPoint", "address")]
    public class EntryPointFunctionBase : FunctionMessage
    {

    }

    public partial class GetAddressFunction : GetAddressFunctionBase { }

    [Function("getAddress", "address")]
    public class GetAddressFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("bytes32", "salt", 2)]
        public virtual byte[] Salt { get; set; }
        [Parameter("bytes32[]", "moduleIds", 3)]
        public virtual List<byte[]> ModuleIds { get; set; }
    }

    public partial class GetAdminCountFunction : GetAdminCountFunctionBase { }

    [Function("getAdminCount", "uint256")]
    public class GetAdminCountFunctionBase : FunctionMessage
    {

    }

    public partial class GetAdminsFunction : GetAdminsFunctionBase { }

    [Function("getAdmins", "address[]")]
    public class GetAdminsFunctionBase : FunctionMessage
    {

    }

    public partial class GetDomainSeparatorFunction : GetDomainSeparatorFunctionBase { }

    [Function("getDomainSeparator", "bytes32")]
    public class GetDomainSeparatorFunctionBase : FunctionMessage
    {

    }

    public partial class GetModuleAddressFunction : GetModuleAddressFunctionBase { }

    [Function("getModuleAddress", "address")]
    public class GetModuleAddressFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "moduleId", 1)]
        public virtual byte[] ModuleId { get; set; }
    }

    public partial class GetRegisteredModuleCountFunction : GetRegisteredModuleCountFunctionBase { }

    [Function("getRegisteredModuleCount", "uint256")]
    public class GetRegisteredModuleCountFunctionBase : FunctionMessage
    {

    }

    public partial class GetRegisteredModulesFunction : GetRegisteredModulesFunctionBase { }

    [Function("getRegisteredModules", "bytes32[]")]
    public class GetRegisteredModulesFunctionBase : FunctionMessage
    {

    }

    public partial class ImplementationFunction : ImplementationFunctionBase { }

    [Function("implementation", "address")]
    public class ImplementationFunctionBase : FunctionMessage
    {

    }

    public partial class IsAdminFunction : IsAdminFunctionBase { }

    [Function("isAdmin", "bool")]
    public class IsAdminFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class IsDeployedFunction : IsDeployedFunctionBase { }

    [Function("isDeployed", "bool")]
    public class IsDeployedFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("bytes32", "salt", 2)]
        public virtual byte[] Salt { get; set; }
        [Parameter("bytes32[]", "moduleIds", 3)]
        public virtual List<byte[]> ModuleIds { get; set; }
    }

    public partial class NonceFunction : NonceFunctionBase { }

    [Function("nonce", "uint256")]
    public class NonceFunctionBase : FunctionMessage
    {

    }

    public partial class RegisterModuleFunction : RegisterModuleFunctionBase { }

    [Function("registerModule")]
    public class RegisterModuleFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "moduleId", 1)]
        public virtual byte[] ModuleId { get; set; }
        [Parameter("address", "moduleAddress", 2)]
        public virtual string ModuleAddress { get; set; }
        [Parameter("uint256", "deadline", 3)]
        public virtual BigInteger Deadline { get; set; }
        [Parameter("bytes[]", "signatures", 4)]
        public virtual List<byte[]> Signatures { get; set; }
    }

    public partial class ThresholdFunction : ThresholdFunctionBase { }

    [Function("threshold", "uint256")]
    public class ThresholdFunctionBase : FunctionMessage
    {

    }

    public partial class UnregisterModuleFunction : UnregisterModuleFunctionBase { }

    [Function("unregisterModule")]
    public class UnregisterModuleFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "moduleId", 1)]
        public virtual byte[] ModuleId { get; set; }
        [Parameter("uint256", "deadline", 2)]
        public virtual BigInteger Deadline { get; set; }
        [Parameter("bytes[]", "signatures", 3)]
        public virtual List<byte[]> Signatures { get; set; }
    }

    public partial class UpdateAdminsFunction : UpdateAdminsFunctionBase { }

    [Function("updateAdmins")]
    public class UpdateAdminsFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "newAdmins", 1)]
        public virtual List<string> NewAdmins { get; set; }
        [Parameter("uint256", "newThreshold", 2)]
        public virtual BigInteger NewThreshold { get; set; }
        [Parameter("uint256", "deadline", 3)]
        public virtual BigInteger Deadline { get; set; }
        [Parameter("bytes[]", "signatures", 4)]
        public virtual List<byte[]> Signatures { get; set; }
    }

    public partial class ValidateSignaturesFunction : ValidateSignaturesFunctionBase { }

    [Function("validateSignatures", "bool")]
    public class ValidateSignaturesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "digest", 1)]
        public virtual byte[] Digest { get; set; }
        [Parameter("bytes[]", "signatures", 2)]
        public virtual List<byte[]> Signatures { get; set; }
    }

    public partial class MaxAdminsOutputDTO : MaxAdminsOutputDTOBase { }

    [FunctionOutput]
    public class MaxAdminsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
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

    public partial class GetAddressOutputDTO : GetAddressOutputDTOBase { }

    [FunctionOutput]
    public class GetAddressOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetAdminCountOutputDTO : GetAdminCountOutputDTOBase { }

    [FunctionOutput]
    public class GetAdminCountOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetAdminsOutputDTO : GetAdminsOutputDTOBase { }

    [FunctionOutput]
    public class GetAdminsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address[]", "", 1)]
        public virtual List<string> ReturnValue1 { get; set; }
    }

    public partial class GetDomainSeparatorOutputDTO : GetDomainSeparatorOutputDTOBase { }

    [FunctionOutput]
    public class GetDomainSeparatorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class GetModuleAddressOutputDTO : GetModuleAddressOutputDTOBase { }

    [FunctionOutput]
    public class GetModuleAddressOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetRegisteredModuleCountOutputDTO : GetRegisteredModuleCountOutputDTOBase { }

    [FunctionOutput]
    public class GetRegisteredModuleCountOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetRegisteredModulesOutputDTO : GetRegisteredModulesOutputDTOBase { }

    [FunctionOutput]
    public class GetRegisteredModulesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32[]", "", 1)]
        public virtual List<byte[]> ReturnValue1 { get; set; }
    }

    public partial class ImplementationOutputDTO : ImplementationOutputDTOBase { }

    [FunctionOutput]
    public class ImplementationOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class IsAdminOutputDTO : IsAdminOutputDTOBase { }

    [FunctionOutput]
    public class IsAdminOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsDeployedOutputDTO : IsDeployedOutputDTOBase { }

    [FunctionOutput]
    public class IsDeployedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class NonceOutputDTO : NonceOutputDTOBase { }

    [FunctionOutput]
    public class NonceOutputDTOBase : IFunctionOutputDTO 
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





    public partial class ValidateSignaturesOutputDTO : ValidateSignaturesOutputDTOBase { }

    [FunctionOutput]
    public class ValidateSignaturesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class AccountCreatedEventDTO : AccountCreatedEventDTOBase { }

    [Event("AccountCreated")]
    public class AccountCreatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "owner", 2, true )]
        public virtual string Owner { get; set; }
        [Parameter("bytes32", "salt", 3, true )]
        public virtual byte[] Salt { get; set; }
        [Parameter("bytes32[]", "moduleIds", 4, false )]
        public virtual List<byte[]> ModuleIds { get; set; }
    }

    public partial class AdminsUpdatedEventDTO : AdminsUpdatedEventDTOBase { }

    [Event("AdminsUpdated")]
    public class AdminsUpdatedEventDTOBase : IEventDTO
    {
        [Parameter("address[]", "newAdmins", 1, false )]
        public virtual List<string> NewAdmins { get; set; }
        [Parameter("uint256", "newThreshold", 2, false )]
        public virtual BigInteger NewThreshold { get; set; }
        [Parameter("uint256", "nonce", 3, false )]
        public virtual BigInteger Nonce { get; set; }
    }

    public partial class ModuleRegisteredEventDTO : ModuleRegisteredEventDTOBase { }

    [Event("ModuleRegistered")]
    public class ModuleRegisteredEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "moduleId", 1, true )]
        public virtual byte[] ModuleId { get; set; }
        [Parameter("address", "moduleAddress", 2, true )]
        public virtual string ModuleAddress { get; set; }
    }

    public partial class ModuleUnregisteredEventDTO : ModuleUnregisteredEventDTOBase { }

    [Event("ModuleUnregistered")]
    public class ModuleUnregisteredEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "moduleId", 1, true )]
        public virtual byte[] ModuleId { get; set; }
    }
}
