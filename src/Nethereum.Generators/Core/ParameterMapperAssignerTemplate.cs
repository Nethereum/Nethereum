using System;
using System.Collections.Generic;

namespace Nethereum.Generators.Core
{
    public class ParameterMapperAssignerTemplate<TParameterModelFrom, TParameterModelTo,
        TParameterFrom, TParameterTo>
        where TParameterFrom : Parameter
        where TParameterModelFrom : ParameterModel<TParameterFrom>, new()
        where TParameterTo:Parameter
        where TParameterModelTo : ParameterModel<TParameterTo>, new()
    {

        public virtual string GenerateMappingAssigment(
            ParameterMap<TParameterFrom, TParameterTo> map,
            string variableSourceName, string destinationVariableName)
        {
            return destinationVariableName + "." + GenerateMappingAssigment(map, variableSourceName);
        }

        public virtual string GenerateMappingAssigment(
            ParameterMap<TParameterFrom, TParameterTo> map, string variableSourceName)

        {
            var modelSource = new TParameterModelFrom();
            //NOTE Cannot use object initialiser Javascript
            modelSource.Parameter = map.From;
            var modelTo = new TParameterModelTo();
            modelTo.Parameter = map.To;

            var converterFormatString = GetConversionFormatString(map.From.Type, map.To.Type);
            if (converterFormatString != null)
            {
                var qualifiedName = $"{variableSourceName}.{modelSource.GetPropertyName()}";
                var conversion = String.Format(converterFormatString, qualifiedName);
                return $@"{modelTo.GetPropertyName()} = {conversion}";
            }

            return $@"{modelTo.GetPropertyName()} = {variableSourceName}.{modelSource.GetPropertyName()}";
        }

        public virtual string GenerateMappingsReturn(
            ParameterMap<TParameterFrom, TParameterTo> map, string variableSourceName, string destinationVariableName)
        {
            return destinationVariableName + "." + GenerateMappingsReturn(map, variableSourceName);
        }

        public virtual string GenerateMappingsReturn(
            ParameterMap<TParameterFrom, TParameterTo> map, string variableSourceName)

        {
            //NOTE Cannot use object initialiser Javascript
            var modelSource = new TParameterModelFrom();
            modelSource.Parameter = map.From;
            var modelTo = new TParameterModelTo();
            modelTo.Parameter = map.To;

            var converterFormatString = GetConversionFormatString(map.To.Type, map.From.Type);
            if (converterFormatString != null)
            {
                var qualifiedName = $"{variableSourceName}.{modelTo.GetPropertyName()}";
                var conversion = String.Format(converterFormatString, qualifiedName);
                return $@"{modelSource.GetPropertyName()} = {conversion}";
            }

            return $@"{modelSource.GetPropertyName()} = {variableSourceName}.{modelTo.GetPropertyName()}";
        }

        protected Dictionary<string, Dictionary<string, string>> ConversionFormatStrings = new Dictionary<string, Dictionary<string, string>>();

        protected virtual string GetConversionFormatString(string typeFrom, string typeTo)
        {
            if (ConversionFormatStrings.ContainsKey(typeFrom) &&
                ConversionFormatStrings[typeFrom].ContainsKey(typeTo))
            {
                return ConversionFormatStrings[typeFrom][typeTo];
            }

            return null;
        }

        protected void AddConversionFormatString(string fromType, string toType, string conversionTemplate)
        {
            ConversionFormatStrings.Add(toType, new Dictionary<string, string>
            {
                {fromType, conversionTemplate}
            });   
        }
    }
}