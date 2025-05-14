using Nethereum.AccountAbstraction.BaseAccount.ContractDefinition;
using Nethereum.Contracts;
using System.Numerics;

namespace Nethereum.AccountAbstraction.BaseAccount
{
    public interface IAccountAbstractionExecuteBuilder
    {
        Task<byte[]> CreateAccountAbstractionExecuteEncodedFunction<TFunctionCall>(string target, BigInteger value, TFunctionCall functionCall) where TFunctionCall : FunctionMessage, new();
    }
}