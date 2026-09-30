using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.AccountAbstraction.Execution
{
    public interface IExecuteEncoder
    {
        byte[] EncodeExecute(string target, BigInteger value, byte[] callData);

        byte[] EncodeBatch(IReadOnlyList<(string target, BigInteger value, byte[] callData)> calls);
    }
}
