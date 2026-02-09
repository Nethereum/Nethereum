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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IValidator.ContractDefinition
{


    public partial class IValidatorDeployment : IValidatorDeploymentBase
    {
        public IValidatorDeployment() : base(BYTECODE) { }
        public IValidatorDeployment(string byteCode) : base(byteCode) { }
    }

    public class IValidatorDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IValidatorDeploymentBase() : base(BYTECODE) { }
        public IValidatorDeploymentBase(string byteCode) : base(byteCode) { }

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
        [Parameter("uint256", "moduleTypeId", 1)]
        public virtual BigInteger ModuleTypeId { get; set; }
    }

    public partial class IsValidSignatureWithSenderFunction : IsValidSignatureWithSenderFunctionBase { }

    [Function("isValidSignatureWithSender", "bytes4")]
    public class IsValidSignatureWithSenderFunctionBase : FunctionMessage
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("bytes32", "hash", 2)]
        public virtual byte[] Hash { get; set; }
        [Parameter("bytes", "signature", 3)]
        public virtual byte[] Signature { get; set; }
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
        [Parameter("bytes", "data", 1)]
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
        [Parameter("bytes4", "magicValue", 1)]
        public virtual byte[] MagicValue { get; set; }
    }






}
