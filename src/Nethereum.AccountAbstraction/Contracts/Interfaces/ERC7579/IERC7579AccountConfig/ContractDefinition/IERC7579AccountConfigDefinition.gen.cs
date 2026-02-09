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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579AccountConfig.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579AccountConfig.ContractDefinition
{


    public partial class IERC7579AccountConfigDeployment : IERC7579AccountConfigDeploymentBase
    {
        public IERC7579AccountConfigDeployment() : base(BYTECODE) { }
        public IERC7579AccountConfigDeployment(string byteCode) : base(byteCode) { }
    }

    public class IERC7579AccountConfigDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IERC7579AccountConfigDeploymentBase() : base(BYTECODE) { }
        public IERC7579AccountConfigDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class AccountIdFunction : AccountIdFunctionBase { }

    [Function("accountId", "string")]
    public class AccountIdFunctionBase : FunctionMessage
    {

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

    public partial class AccountIdOutputDTO : AccountIdOutputDTOBase { }

    [FunctionOutput]
    public class AccountIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
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
}
