using Nethereum.Generators.Core;

namespace Nethereum.Generators.Model
{
    public class StructABI : IMessage<ParameterABI>
    {
        public StructABI(string name)
        {
            Name = name;
        }

        public ParameterABI[] InputParameters { get; set; }

        public string Name { get; set; }
    }
}