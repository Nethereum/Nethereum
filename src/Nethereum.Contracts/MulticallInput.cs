using System.Numerics;

namespace Nethereum.Contracts
{
    public class MulticallInput<TFunctionMessage> : IMulticallInput
       where TFunctionMessage : FunctionMessage, new()
    {
        public MulticallInput(TFunctionMessage functionMessage, string contractAddressTarget)
        {
            Target = contractAddressTarget;
            Input = functionMessage;
        }

        /// <summary>
        /// Same smart contract target for all calls, no need to provide a target
        /// </summary>
        public MulticallInput(TFunctionMessage functionMessage)
        {
            Input = functionMessage;
        }

        public BigInteger Value { get; set; } = 0;
        public string Target { get; set; }
        public TFunctionMessage Input { get; set; }

        public byte[] GetCallData()
        {
            return Input.GetCallData();
        }

    }
}