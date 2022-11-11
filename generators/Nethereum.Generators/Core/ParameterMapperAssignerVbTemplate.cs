namespace Nethereum.Generators.Core
{
    public class ParameterMapperAssignerVbTemplate<TParameterModelFrom, TParameterModelTo,
        TParameterFrom, TParameterTo> : ParameterMapperAssignerTemplate<TParameterModelFrom, TParameterModelTo, TParameterFrom, TParameterTo>
        where TParameterFrom : Parameter
        where TParameterModelFrom : ParameterModel<TParameterFrom>, new()
        where TParameterTo : Parameter
        where TParameterModelTo : ParameterModel<TParameterTo>, new()
    {

    }
}