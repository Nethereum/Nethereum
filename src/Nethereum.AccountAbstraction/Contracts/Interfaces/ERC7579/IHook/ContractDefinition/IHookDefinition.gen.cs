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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IHook.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IHook.ContractDefinition
{


    public partial class IHookDeployment : IHookDeploymentBase
    {
        public IHookDeployment() : base(BYTECODE) { }
        public IHookDeployment(string byteCode) : base(byteCode) { }
    }

    public class IHookDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IHookDeploymentBase() : base(BYTECODE) { }
        public IHookDeploymentBase(string byteCode) : base(byteCode) { }

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








}
