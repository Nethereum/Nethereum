using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ethereum.RPC.ABI.Attributes;
using Ethereum.RPC.Util;
using System.Reflection;

namespace Ethereum.RPC.ABI
{
    public class FunctionCallDecoder
    {
        public T DecodeOutput<T>(string output) where T : new()
        {
            var type = typeof(T);

            var function = type.GetTypeInfo().GetCustomAttribute<FunctionOutputAttribute>();
            if (function == null)
                throw new ArgumentException("Generic Type should have a Function Ouput Attribute");


            var properties = type.GetProperties();

            var parameterObjects = new List<ParameterOutputProperty>();

            foreach (var property in properties)
            {
                if (property.IsDefined(typeof(ParameterAttribute), false))
                {
                    var parameterAttribute = property.GetCustomAttribute<ParameterAttribute>();
                    parameterObjects.Add(new ParameterOutputProperty() { Parameter = parameterAttribute.Parameter, PropertyInfo = property, DecodedType = property.PropertyType});
                }
            }
            var orderedParameters= parameterObjects.OrderBy(x => x.Parameter.Order).ToArray();
            var parameterResults = DecodeOutput(output, orderedParameters);
            var result= new T();

            parameterResults.ForEach(x =>
            {
                var parameter = (ParameterOutputProperty) x;
                var propertyInfo = parameter.PropertyInfo;
                propertyInfo.SetValue(result, parameter.Result);

            });

            return result;
            
        }


        public List<ParameterOutput> DecodeOutput(string output, params Parameter[] outputParameters)
        {
            var results = outputParameters.Select(param => new ParameterOutput() {Parameter = param}).ToArray();
            return DecodeOutput(output, results);
        }


        public List<ParameterOutput> DecodeOutput(string output, params ParameterOutput[] outputParameters)
        {
          
            byte[] outputBytes = output.HexToByteArray();

            var currentIndex = 0;

            foreach (var outputParam in outputParameters)
            {
                var param = outputParam.Parameter;
                if (param.ABIType.IsDynamic())
                {
                    outputParam.DataIndexStart = EncoderDecoderHelpers.GetNumberOfBytes(outputBytes.Skip(currentIndex).ToArray());
                    currentIndex = currentIndex + 32;
                }
                else
                {
                    var bytes = outputBytes.Skip(currentIndex).Take(param.ABIType.FixedSize).ToArray();
                    outputParam.Result = param.ABIType.Decode(bytes, outputParam.DecodedType);

                    currentIndex = currentIndex + param.ABIType.FixedSize;
                }
            }

            ParameterOutput currentDataItem = null;
            foreach (var nextDataItem in outputParameters.Where(outputParam => outputParam.Parameter.ABIType.IsDynamic()))
            {
                if (currentDataItem != null)
                {
                    var bytes =
                        outputBytes.Skip(currentDataItem.DataIndexStart).Take(nextDataItem.DataIndexStart).ToArray();
                    currentDataItem.Result = currentDataItem.Parameter.ABIType.Decode(bytes, currentDataItem.DecodedType);
                }
                currentDataItem = nextDataItem;
            }

            if (currentDataItem != null)
            {
                var bytes = outputBytes.Skip(currentDataItem.DataIndexStart).ToArray();
                currentDataItem.Result = currentDataItem.Parameter.ABIType.Decode(bytes, currentDataItem.DecodedType);
            }
            return outputParameters.ToList();
        }
    }
}