using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Contracts.QueryHandlers.MultiCall
{
    public class MulticallInputOutput<TFunctionMessage, TFunctionOutput> : IMulticallInputOutput
        where TFunctionMessage : FunctionMessage, new()
        where TFunctionOutput : IFunctionOutputDTO, new()
    {
        public MulticallInputOutput(TFunctionMessage functionMessage, string contractAddressTarget)
        {
            this.Target = contractAddressTarget;
            this.Input = functionMessage;
        }

        public string Target { get; set; }
        public TFunctionMessage Input { get; set; }
        public TFunctionOutput Output { get; private set; }
        public byte[] RawOutput { get; private set; }

        public byte[] GetCallData()
        {
            return Input.GetCallData();
        }

        public void Decode(byte[] output)
        {
            Output = new TFunctionOutput().DecodeOutput(output.ToHex());
            RawOutput = output;
        }
    }
}