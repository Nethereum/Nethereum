using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;

namespace Nethereum.AccountAbstraction.Execution
{
    public class SimpleAccountExecuteEncoder : IExecuteEncoder
    {
        [Function("execute")]
        private class ExecuteFunction : FunctionMessage
        {
            [Parameter("address", "target", 1)]
            public virtual string Target { get; set; }
            [Parameter("uint256", "value", 2)]
            public virtual BigInteger Value { get; set; }
            [Parameter("bytes", "data", 3)]
            public virtual byte[] Data { get; set; }
        }

        [Function("executeBatch")]
        private class ExecuteBatchFunction : FunctionMessage
        {
            [Parameter("tuple[]", "calls", 1)]
            public virtual List<Call> Calls { get; set; }
        }

        public byte[] EncodeExecute(string target, BigInteger value, byte[] callData)
        {
            var executeFunction = new ExecuteFunction
            {
                Target = target,
                Value = value,
                Data = callData
            };
            return executeFunction.GetCallData();
        }

        public byte[] EncodeBatch(IReadOnlyList<(string target, BigInteger value, byte[] callData)> calls)
        {
            var callList = calls.Select(c => new Call
            {
                Target = c.target,
                Value = c.value,
                Data = c.callData
            }).ToList();

            var executeBatchFunction = new ExecuteBatchFunction { Calls = callList };
            return executeBatchFunction.GetCallData();
        }
    }
}
