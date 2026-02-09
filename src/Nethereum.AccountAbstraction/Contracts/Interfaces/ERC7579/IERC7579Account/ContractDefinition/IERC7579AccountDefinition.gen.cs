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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579Account.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579Account.ContractDefinition
{


    public partial class IERC7579AccountDeployment : IERC7579AccountDeploymentBase
    {
        public IERC7579AccountDeployment() : base(BYTECODE) { }
        public IERC7579AccountDeployment(string byteCode) : base(byteCode) { }
    }

    public class IERC7579AccountDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IERC7579AccountDeploymentBase() : base(BYTECODE) { }
        public IERC7579AccountDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class AccountIdFunction : AccountIdFunctionBase { }

    [Function("accountId", "string")]
    public class AccountIdFunctionBase : FunctionMessage
    {

    }

    public partial class ExecuteFunction : ExecuteFunctionBase { }

    [Function("execute")]
    public class ExecuteFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "mode", 1)]
        public virtual byte[] Mode { get; set; }
        [Parameter("bytes", "executionCalldata", 2)]
        public virtual byte[] ExecutionCalldata { get; set; }
    }

    public partial class ExecuteFromExecutorFunction : ExecuteFromExecutorFunctionBase { }

    [Function("executeFromExecutor", "bytes[]")]
    public class ExecuteFromExecutorFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "mode", 1)]
        public virtual byte[] Mode { get; set; }
        [Parameter("bytes", "executionCalldata", 2)]
        public virtual byte[] ExecutionCalldata { get; set; }
    }

    public partial class InstallModuleFunction : InstallModuleFunctionBase { }

    [Function("installModule")]
    public class InstallModuleFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "moduleTypeId", 1)]
        public virtual BigInteger ModuleTypeId { get; set; }
        [Parameter("address", "module", 2)]
        public virtual string Module { get; set; }
        [Parameter("bytes", "initData", 3)]
        public virtual byte[] InitData { get; set; }
    }

    public partial class IsModuleInstalledFunction : IsModuleInstalledFunctionBase { }

    [Function("isModuleInstalled", "bool")]
    public class IsModuleInstalledFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "moduleTypeId", 1)]
        public virtual BigInteger ModuleTypeId { get; set; }
        [Parameter("address", "module", 2)]
        public virtual string Module { get; set; }
        [Parameter("bytes", "additionalContext", 3)]
        public virtual byte[] AdditionalContext { get; set; }
    }

    public partial class IsValidSignatureFunction : IsValidSignatureFunctionBase { }

    [Function("isValidSignature", "bytes4")]
    public class IsValidSignatureFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "hash", 1)]
        public virtual byte[] Hash { get; set; }
        [Parameter("bytes", "signature", 2)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class SupportsExecutionModeFunction : SupportsExecutionModeFunctionBase { }

    [Function("supportsExecutionMode", "bool")]
    public class SupportsExecutionModeFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "encodedMode", 1)]
        public virtual byte[] EncodedMode { get; set; }
    }

    public partial class SupportsModuleFunction : SupportsModuleFunctionBase { }

    [Function("supportsModule", "bool")]
    public class SupportsModuleFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "moduleTypeId", 1)]
        public virtual BigInteger ModuleTypeId { get; set; }
    }

    public partial class UninstallModuleFunction : UninstallModuleFunctionBase { }

    [Function("uninstallModule")]
    public class UninstallModuleFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "moduleTypeId", 1)]
        public virtual BigInteger ModuleTypeId { get; set; }
        [Parameter("address", "module", 2)]
        public virtual string Module { get; set; }
        [Parameter("bytes", "deInitData", 3)]
        public virtual byte[] DeInitData { get; set; }
    }

    public partial class AccountIdOutputDTO : AccountIdOutputDTOBase { }

    [FunctionOutput]
    public class AccountIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }







    public partial class IsModuleInstalledOutputDTO : IsModuleInstalledOutputDTOBase { }

    [FunctionOutput]
    public class IsModuleInstalledOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsValidSignatureOutputDTO : IsValidSignatureOutputDTOBase { }

    [FunctionOutput]
    public class IsValidSignatureOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes4", "magicValue", 1)]
        public virtual byte[] MagicValue { get; set; }
    }

    public partial class SupportsExecutionModeOutputDTO : SupportsExecutionModeOutputDTOBase { }

    [FunctionOutput]
    public class SupportsExecutionModeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class SupportsModuleOutputDTO : SupportsModuleOutputDTOBase { }

    [FunctionOutput]
    public class SupportsModuleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }



    public partial class ModuleInstalledEventDTO : ModuleInstalledEventDTOBase { }

    [Event("ModuleInstalled")]
    public class ModuleInstalledEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "moduleTypeId", 1, false )]
        public virtual BigInteger ModuleTypeId { get; set; }
        [Parameter("address", "module", 2, false )]
        public virtual string Module { get; set; }
    }

    public partial class ModuleUninstalledEventDTO : ModuleUninstalledEventDTOBase { }

    [Event("ModuleUninstalled")]
    public class ModuleUninstalledEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "moduleTypeId", 1, false )]
        public virtual BigInteger ModuleTypeId { get; set; }
        [Parameter("address", "module", 2, false )]
        public virtual string Module { get; set; }
    }
}
