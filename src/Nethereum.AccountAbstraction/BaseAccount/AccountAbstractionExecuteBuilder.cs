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
    public class AccountAbstractionExecuteBuilder
    {
        public static byte[] CreateAccountAbstractionExecuteEncodedFunction<TFunctionCall>(string target, BigInteger value, TFunctionCall functionCall)
            where TFunctionCall : FunctionMessage, new()
        {
            var executeFunction = new ExecuteFunction();
            executeFunction.Target = target;
            executeFunction.Value = value;
            executeFunction.Data = functionCall.GetCallData();
            return executeFunction.GetCallData();
        }

        public static Call CreateAccountAbstractionCall<TFunctionCall>(string target, BigInteger value, TFunctionCall functionCall)
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

        public static byte[] CreateAccountAbstractionExecuteBatchEncodedFunction<TFunctionCall>(List<Call> calls)
            where TFunctionCall : FunctionMessage, new()
        {
            var executeBatchFunction = new ExecuteBatchFunction();
            executeBatchFunction.Calls = calls;
            return executeBatchFunction.GetCallData();
        }

    }
}
