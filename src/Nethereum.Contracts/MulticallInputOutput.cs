using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Contracts
{
    public class MulticallInputOutput<TFunctionMessage, TFunctionOutput> : MulticallInput<TFunctionMessage>, IMulticallInputOutput
        where TFunctionMessage : FunctionMessage, new()
        where TFunctionOutput : IFunctionOutputDTO, new()
    {
        public MulticallInputOutput(TFunctionMessage functionMessage, string contractAddressTarget):base(functionMessage, contractAddressTarget)
        {

        }
        public bool Success { get; set; }
        public bool AllowFailure { get; set; } = false;
        public TFunctionOutput Output { get; private set; }
        public byte[] RawOutput { get; private set; }

        public void Decode(byte[] output)
        {
            Output = new TFunctionOutput().DecodeOutput(output.ToHex());
            RawOutput = output;
        }
    }
}