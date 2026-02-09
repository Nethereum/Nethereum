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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IExecutor.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IExecutor.ContractDefinition
{


    public partial class IExecutorDeployment : IExecutorDeploymentBase
    {
        public IExecutorDeployment() : base(BYTECODE) { }
        public IExecutorDeployment(string byteCode) : base(byteCode) { }
    }

    public class IExecutorDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IExecutorDeploymentBase() : base(BYTECODE) { }
        public IExecutorDeploymentBase(string byteCode) : base(byteCode) { }

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
