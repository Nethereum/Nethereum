using System;
using System.Numerics;

namespace Nethereum.X402.Blockchain;

internal sealed class X402SettlementGasExceededException : Exception
{
    public X402SettlementGasExceededException(BigInteger estimate, long limit)
        : base($"The settlement transaction needs an estimated {estimate} gas, above the configured limit of {limit}.")
    {
        Estimate = estimate;
        Limit = limit;
    }

    public BigInteger Estimate { get; }

    public long Limit { get; }
}
