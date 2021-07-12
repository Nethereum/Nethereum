using System;
using System.Collections;
using System.Numerics;

namespace Nethereum.JsonRpc.UnityClient
{
    public interface IFee1559CalculationUnityRequestStrategy
    {
        IEnumerator CalculateFee(BigInteger? maxPriorityFeePerGas = null);
        Fee1559 Result { get; set; }
        Exception Exception { get; set; }
    }
}