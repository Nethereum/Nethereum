using System;
using System.Collections.Generic;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;

namespace Nethereum.ABI
{
    public class TupleTypeDecoder : TypeDecoder
    {
        public Parameter[] Components { get; set; }

        private readonly ParameterDecoder parameterDecoder;

        public TupleTypeDecoder()
        {
            parameterDecoder = new ParameterDecoder();
        }

        public override object Decode(byte[] encoded, Type type)
        {
            //TODO: do we need to check ? we always return a list of ParameterOutputs
            // if (!IsSupportedType(type)) throw new NotSupportedException(type + " is not supported");
            var decodingComponents = InitDefaultDecodingComponents();
            return parameterDecoder.DecodeOutput(encoded, decodingComponents);
        }

        public ParameterOutput[] InitDefaultDecodingComponents()
        {
            var decodingDefaultComponents = new List<ParameterOutput>();
            foreach (var component in Components)
            {
                var parameterOutput = new ParameterOutput();
                parameterOutput.Parameter = component;
                if (component.DecodedType == null)
                    parameterOutput.Parameter.DecodedType = component.ABIType.GetDefaultDecodingType();
                decodingDefaultComponents.Add(parameterOutput);
            }
            return decodingDefaultComponents.ToArray();
        }

        public List<ParameterOutput> Decode(byte[] encoded)
        {
            return Decode<List<ParameterOutput>>(encoded);
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(List<ParameterOutput>);
        }

        public override bool IsSupportedType(Type type)
        {
            return (type == typeof(List<ParameterOutput>));
        }
    }
}