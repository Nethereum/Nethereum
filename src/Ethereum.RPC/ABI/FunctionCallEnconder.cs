using System;
using System.Collections.Generic;
using Ethereum.RPC.Util;
using System.Linq;
using System.Numerics;

namespace Ethereum.RPC.ABI
{

    public class Parameter
    {
        public string Name { get; set; }
        public ABIType Type { get; set; }
    }

    public class ParameterOutputResult

    {
        public Parameter ParameterOutput { get; set; }
        public int DataIndexStart { get; set; }

        public object Result { get; set; }
    }

    public class FunctionCallEnconder
    {
        public string FunctionSha3Encoded { get; set; }

        public Parameter[] InputsParameters { get; set; }
        public Parameter[] OutputParameters { get; set; }

        public string EncodeRequest(params object[] parametersValues)
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

            if (parametersValues.Length > InputsParameters.Length)
            {
                throw new Exception("Too many arguments: " + parametersValues.Length + " > " + InputsParameters.Length);
            }

            int staticSize = 0;
            int dynamicCount = 0;
            // calculating static size and number of dynamic params
            for (int i = 0; i < parametersValues.Length; i++)
            {
                var inputsParameter = InputsParameters[i];
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
                if (InputsParameters[i].Type.IsDynamic())
                {
                    byte[] dynamicValueBytes = InputsParameters[i].Type.Encode(parametersValues[i]);
                    encodedBytes[i] = IntType.EncodeInt(currentDynamicPointer);
                    encodedBytes[parametersValues.Length + currentDynamicCount] = dynamicValueBytes;
                    currentDynamicCount++;
                    currentDynamicPointer += dynamicValueBytes.Length;
                }
                else
                {
                    encodedBytes[i] = InputsParameters[i].Type.Encode(parametersValues[i]);
                }
            }
            return ByteUtil.Merge(encodedBytes);

        }

        public List<ParameterOutputResult> DecodeOutput(string output)
        {
            var results = new List<ParameterOutputResult>();
            byte[] outputBytes = output.HexStringToByteArray();

            var currentIndex = 0;

            foreach (var outputParam in OutputParameters)
            {
                var parameterOutputResult = new ParameterOutputResult() {ParameterOutput = outputParam};
                results.Add(parameterOutputResult);

                if (outputParam.Type.IsDynamic())
                {
                    parameterOutputResult.DataIndexStart = EncoderDecoderHelpes.GetNumberOfBytes(outputBytes.Skip(currentIndex).ToArray());
                    currentIndex = currentIndex + 32;
                }
                else
                {
                    var bytes = outputBytes.Skip(currentIndex).Take(outputParam.Type.FixedSize).ToArray();
                    parameterOutputResult.Result = outputParam.Type.Decode(bytes);

                    currentIndex = currentIndex + outputParam.Type.FixedSize;
                }
            }

            ParameterOutputResult currentDataItem = null;
            foreach (var nextDataItem in results.Where(outputParam => outputParam.ParameterOutput.Type.IsDynamic()))
            {
                if (currentDataItem != null)
                {
                    var bytes =
                        outputBytes.Skip(currentDataItem.DataIndexStart).Take(nextDataItem.DataIndexStart).ToArray();
                    currentDataItem.Result = currentDataItem.ParameterOutput.Type.Decode(bytes);
                }
                currentDataItem = nextDataItem;
            }

            if (currentDataItem != null)
            {
                var bytes = outputBytes.Skip(currentDataItem.DataIndexStart).ToArray();
                currentDataItem.Result = currentDataItem.ParameterOutput.Type.Decode(bytes);
            }
            return results;
        }
    }
}