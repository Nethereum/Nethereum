using Nethereum.Generators.Core;

namespace Nethereum.Generators.Model
{
    public class FunctionABI: IMessage<ParameterABI>
    {
        public FunctionABI(string name, bool constant, bool serpent = false)
        {
            Name = name;
            Serpent = serpent;
            Constant = constant;
        }

        public bool Serpent { get; private set; }

        public bool Constant { get; private set; }

        public string Name { get; set; }

        public ParameterABI[] InputParameters { get; set; }
        public ParameterABI[] OutputParameters { get; set; }
    }
}