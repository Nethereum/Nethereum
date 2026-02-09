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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579ModuleConfig.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579ModuleConfig.ContractDefinition
{


    public partial class IERC7579ModuleConfigDeployment : IERC7579ModuleConfigDeploymentBase
    {
        public IERC7579ModuleConfigDeployment() : base(BYTECODE) { }
        public IERC7579ModuleConfigDeployment(string byteCode) : base(byteCode) { }
    }

    public class IERC7579ModuleConfigDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IERC7579ModuleConfigDeploymentBase() : base(BYTECODE) { }
        public IERC7579ModuleConfigDeploymentBase(string byteCode) : base(byteCode) { }

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



    public partial class IsModuleInstalledOutputDTO : IsModuleInstalledOutputDTOBase { }

    [FunctionOutput]
    public class IsModuleInstalledOutputDTOBase : IFunctionOutputDTO 
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
