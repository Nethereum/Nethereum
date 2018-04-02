namespace Nethereum.Generators.Core
{
    public class ParameterModel<TParameter> where TParameter:Parameter
    {
        public TParameter Parameter { get; }
        protected CommonGenerators CommonGenerators { get; }

        public ParameterModel(TParameter parameter)
        {
            Parameter = parameter;
            CommonGenerators = new CommonGenerators();
        }

        public virtual string GetVariableName()
        {
            return CommonGenerators.GenerateVariableName(Parameter.Name);
        }

        public virtual string GetPropertyName()
        {
            return CommonGenerators.GeneratePropertyName(Parameter.Name);
        }
    }
}