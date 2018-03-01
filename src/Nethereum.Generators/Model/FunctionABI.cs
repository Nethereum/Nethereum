namespace Nethereum.Generators.Model
{
    public class FunctionABI
    {
        public FunctionABI(string name, bool constant, bool serpent = false)
        {
            Name = name;
            Serpent = serpent;
            Constant = constant;
        }

        public bool Serpent { get; private set; }

        public bool Constant { get; private set; }

        public string Name { get; }

        public Parameter[] InputParameters { get; set; }
        public Parameter[] OutputParameters { get; set; }
    }
}