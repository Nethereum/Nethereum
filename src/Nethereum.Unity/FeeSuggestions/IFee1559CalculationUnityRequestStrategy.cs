using System;
using System.Collections;
using System.Numerics;
using Nethereum.RPC.Fee1559Suggestions;

namespace Nethereum.Unity.FeeSuggestions
{
    public interface IFee1559SuggestionUnityRequestStrategy
    {
        IEnumerator SuggestFee(BigInteger? maxPriorityFeePerGas = null);
        Fee1559 Result { get; set; }
        Exception Exception { get; set; }
    }
}