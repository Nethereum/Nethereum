using Ethereum.RPC.Util;

namespace Ethereum.RPC.ABI
{
    public class FunctionCallEnconder
    {
        public string FunctionSha3Encoded { get; set; }

        public string[] FunctionTypes { get; set; }

        public string Encode(params object[] parameters)
        {

            //TODO dynamic types
            var parametersEncoded = "";
            for (var i = 0; i < FunctionTypes.Length; i++)
            {
                parametersEncoded += ABIType.CreateABIType(FunctionTypes[i]).Encode(parameters[i]).ToHexString();
            }

            var prefix = "0x";

            if (FunctionSha3Encoded.StartsWith(prefix))
            {
                prefix = "";
            }

            return prefix + FunctionSha3Encoded + parametersEncoded;
        }

    }
}