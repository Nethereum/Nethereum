using Nethereum.Generators.Core;

namespace Nethereum.Generators.Core
{
    public class ParameterMapperAssignerCSharpTemplate
    {
        public string GenerateMappingAssigment<TParameter1, TParameter2>(ParameterMap<TParameter1, TParameter2> map, string variableSourceName)
            where TParameter1: Parameter
            where TParameter2: Parameter
        {
            var modelSource = new ParameterModel<TParameter1>(map.From);
            var modelTo = new ParameterModel<TParameter2>(map.To);
            return $@"{modelTo.GetPropertyName()} = {variableSourceName}.{modelSource.GetPropertyName()}";
        }


        public string GenerateAllMappingsReturns<TParameter1, TParameter2>(ParameterMap<TParameter1, TParameter2> map, string variableSourceName)
            where TParameter1 : Parameter
            where TParameter2 : Parameter
        {
            var modelFrom = new ParameterModel<TParameter1>(map.From);
            var modelTo = new ParameterModel<TParameter2>(map.To);
            return $@"{modelFrom.GetPropertyName()} = {variableSourceName}.{modelTo.GetPropertyName()}";
        }
    }
}