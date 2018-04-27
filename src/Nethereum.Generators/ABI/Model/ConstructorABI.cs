using Nethereum.Generators.Core;

namespace Nethereum.Generators.Model
{
    public class ConstructorABI:IMessage<ParameterABI>
    {
        public ConstructorABI()
        {
            InputParameters = new ParameterABI[0];
        }

        public string Name { get; set; }

        public ParameterABI[] InputParameters { get; set; }
    }
}