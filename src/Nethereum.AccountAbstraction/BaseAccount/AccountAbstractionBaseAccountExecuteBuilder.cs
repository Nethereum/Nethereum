using Nethereum.AccountAbstraction.BaseAccount.ContractDefinition;
using Nethereum.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.AccountAbstraction.BaseAccount
{



    public class AccountAbstractionBaseAccountExecuteBuilder : IAccountAbstractionExecuteBuilder
    {
        public Task<byte[]> CreateAccountAbstractionExecuteEncodedFunction<TFunctionCall>(string target, BigInteger value, TFunctionCall functionCall)
            where TFunctionCall : FunctionMessage, new()
        {
            var executeFunction = new ExecuteFunction();
            executeFunction.Target = target;
            executeFunction.Value = value;
            executeFunction.Data = functionCall.GetCallData();
            return Task.FromResult(executeFunction.GetCallData());
        }

        public Call CreateAccountAbstractionCall<TFunctionCall>(string target, BigInteger value, TFunctionCall functionCall)
            where TFunctionCall : FunctionMessage, new()
        {
            var call = new Call
            {
                Target = target,
                Value = value,
                Data = functionCall.GetCallData()
            };
            return call;
        }

        public byte[] CreateAccountAbstractionExecuteBatchEncodedFunction<TFunctionCall>(List<Call> calls)
            where TFunctionCall : FunctionMessage, new()
        {
            var executeBatchFunction = new ExecuteBatchFunction();
            executeBatchFunction.Calls = calls;
            return executeBatchFunction.GetCallData();
        }

    }
}
