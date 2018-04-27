using Nethereum.Generators.Core;

namespace Nethereum.Generators.Core
{

    public class ParameterMapperAssignerTemplate
    {

        public virtual string GenerateMappingAssigment<TParameter1, TParameter2>(
            ParameterMap<TParameter1, TParameter2> map, string variableSourceName, string destinationVariableName)
            where TParameter1 : Parameter
            where TParameter2 : Parameter
        {
            return destinationVariableName + "." + GenerateMappingAssigment(map, variableSourceName);
        }

        public virtual string GenerateMappingAssigment<TParameter1, TParameter2>(
            ParameterMap<TParameter1, TParameter2> map, string variableSourceName)
            where TParameter1 : Parameter
            where TParameter2 : Parameter
        {
            var modelSource = new ParameterModel<TParameter1>(map.From);
            var modelTo = new ParameterModel<TParameter2>(map.To);
            return $@"{modelTo.GetPropertyName()} = {variableSourceName}.{modelSource.GetPropertyName()}";
        }

        public virtual string GenerateMappingsReturn<TParameter1, TParameter2>(
            ParameterMap<TParameter1, TParameter2> map, string variableSourceName, string destinationVariableName)
            where TParameter1 : Parameter
            where TParameter2 : Parameter
        {
            return destinationVariableName + "." + GenerateMappingsReturn(map, variableSourceName);
        }

        public virtual string GenerateMappingsReturn<TParameter1, TParameter2>(
            ParameterMap<TParameter1, TParameter2> map, string variableSourceName)
            where TParameter1 : Parameter
            where TParameter2 : Parameter
        {
            var modelFrom = new ParameterModel<TParameter1>(map.From);
            var modelTo = new ParameterModel<TParameter2>(map.To);
            return $@"{modelFrom.GetPropertyName()} = {variableSourceName}.{modelTo.GetPropertyName()}";
        }



    }

    public class ParameterMapperAssignerCSharpTemplate: ParameterMapperAssignerTemplate
    {

    }

    public class ParameterMapperAssignerVbTemplate : ParameterMapperAssignerTemplate
    {

    }
}