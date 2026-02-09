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
using Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountExecute.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountExecute.ContractDefinition
{


    public partial class IAccountExecuteDeployment : IAccountExecuteDeploymentBase
    {
        public IAccountExecuteDeployment() : base(BYTECODE) { }
        public IAccountExecuteDeployment(string byteCode) : base(byteCode) { }
    }

    public class IAccountExecuteDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IAccountExecuteDeploymentBase() : base(BYTECODE) { }
        public IAccountExecuteDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class ExecuteFunction : ExecuteFunctionBase { }

    [Function("execute", "bytes")]
    public class ExecuteFunctionBase : FunctionMessage
    {
        [Parameter("address", "dest", 1)]
        public virtual string Dest { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class ExecuteBatchFunction : ExecuteBatchFunctionBase { }

    [Function("executeBatch", "bytes[]")]
    public class ExecuteBatchFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "dest", 1)]
        public virtual List<string> Dest { get; set; }
        [Parameter("uint256[]", "value", 2)]
        public virtual List<BigInteger> Value { get; set; }
        [Parameter("bytes[]", "data", 3)]
        public virtual List<byte[]> Data { get; set; }
    }

    public partial class ExecuteUserOpFunction : ExecuteUserOpFunctionBase { }

    [Function("executeUserOp")]
    public class ExecuteUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
    }






}
