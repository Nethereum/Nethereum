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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579Execution.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ERC7579.IERC7579Execution.ContractDefinition
{


    public partial class IERC7579ExecutionDeployment : IERC7579ExecutionDeploymentBase
    {
        public IERC7579ExecutionDeployment() : base(BYTECODE) { }
        public IERC7579ExecutionDeployment(string byteCode) : base(byteCode) { }
    }

    public class IERC7579ExecutionDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IERC7579ExecutionDeploymentBase() : base(BYTECODE) { }
        public IERC7579ExecutionDeploymentBase(string byteCode) : base(byteCode) { }

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




}
