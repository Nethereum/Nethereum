using System;
using Ethereum.RPC.Util;

namespace Ethereum.RPC.ABI
{

    public class Param
    {
        public string Name;
        public ABIType Type;
    }

    public class FunctionCallEnconder
    {
        public string FunctionSha3Encoded { get; set; }

        public Param[] InputsParams { get; set; }

        public string Encode(params object[] parametersValues)
        {
            var parametersEncoded = EncodeParameters(parametersValues).ToHexString();

            var prefix = "0x";

            if (FunctionSha3Encoded.StartsWith(prefix))
            {
                prefix = "";
            }

            return prefix + FunctionSha3Encoded + parametersEncoded;
        }

        public byte[] EncodeParameters(params object[] parametersValues)
        {

            if (parametersValues.Length > InputsParams.Length)
            {
                throw new Exception("Too many arguments: " + parametersValues.Length + " > " + InputsParams.Length);
            }

            int staticSize = 0;
            int dynamicCount = 0;
            // calculating static size and number of dynamic params
            for (int i = 0; i < parametersValues.Length; i++)
            {
                var inputsParameter = InputsParams[i];
                int inputParameterSize = inputsParameter.Type.FixedSize;
                if (inputParameterSize < 0)
                {
                    dynamicCount++;
                    staticSize += 32;
                }
                else
                {
                    staticSize += inputParameterSize;
                }
            }

            byte[][] encodedBytes = new byte[parametersValues.Length + dynamicCount][];
          
            int currentDynamicPointer = staticSize;
            int currentDynamicCount = 0;
            for (int i = 0; i < parametersValues.Length; i++)
            {
                if (InputsParams[i].Type.IsDynamic())
                {
                    byte[] dynamicValueBytes = InputsParams[i].Type.Encode(parametersValues[i]);
                    encodedBytes[i] = IntType.EncodeInt(currentDynamicPointer);
                    encodedBytes[parametersValues.Length + currentDynamicCount] = dynamicValueBytes;
                    currentDynamicCount++;
                    currentDynamicPointer += dynamicValueBytes.Length;
                }
                else
                {
                    encodedBytes[i] = InputsParams[i].Type.Encode(parametersValues[i]);
                }
            }
            return ByteUtil.Merge(encodedBytes);

        }

    }
}