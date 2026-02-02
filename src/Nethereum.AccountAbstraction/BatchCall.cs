using System.Numerics;
using Nethereum.Contracts;

namespace Nethereum.AccountAbstraction
{
    public class BatchCall
    {
        public byte[] CallData { get; set; }
        public BigInteger Value { get; set; }

        public BatchCall() { }

        public BatchCall(byte[] callData, BigInteger value = default)
        {
            CallData = callData;
            Value = value;
        }

        public static BatchCall From<TFunctionMessage>(TFunctionMessage message, BigInteger? ethValue = null)
            where TFunctionMessage : FunctionMessage
        {
            return new BatchCall
            {
                CallData = message.GetCallData(),
                Value = ethValue ?? message.AmountToSend
            };
        }
    }

    public static class BatchCallExtensions
    {
        public static BatchCall ToBatchCall<TFunctionMessage>(this TFunctionMessage message, BigInteger? ethValue = null)
            where TFunctionMessage : FunctionMessage
        {
            return BatchCall.From(message, ethValue);
        }
    }
}
