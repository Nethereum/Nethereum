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
            return $@"{modelSource.GetPropertyName()} = {variableSourceName}.{modelTo.GetPropertyName()}";
        }
    }
}