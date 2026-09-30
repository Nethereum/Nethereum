using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.Contracts;
using NethereumAccountExecuteFunction = Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition.ExecuteFunction;

namespace Nethereum.AccountAbstraction.Execution
{
    public class Erc7579ExecuteEncoder : IExecuteEncoder
    {
        public byte[] EncodeExecute(string target, BigInteger value, byte[] callData)
        {
            var mode = ERC7579ModeLib.EncodeSingleDefault();
            var executionCalldata = ERC7579ExecutionLib.EncodeSingle(target, value, callData);
            return EncodeExecuteFunction(mode, executionCalldata);
        }

        public byte[] EncodeBatch(IReadOnlyList<(string target, BigInteger value, byte[] callData)> calls)
        {
            var mode = ERC7579ModeLib.EncodeBatchDefault();
            var callArray = calls.Select(c => new Call
            {
                Target = c.target,
                Value = c.value,
                Data = c.callData
            }).ToArray();
            var executionCalldata = ERC7579ExecutionLib.EncodeBatch(callArray);
            return EncodeExecuteFunction(mode, executionCalldata);
        }

        private static byte[] EncodeExecuteFunction(byte[] mode, byte[] executionCalldata)
        {
            var executeFunction = new NethereumAccountExecuteFunction
            {
                Mode = mode,
                ExecutionCalldata = executionCalldata
            };
            return executeFunction.GetCallData();
        }
    }
}
