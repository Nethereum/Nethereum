namespace Nethereum.Generators.Core
{
    public class Parameter
    {
        public Parameter(string name, string type, int order)
        {
            Name = name;
            Type = type;
            Order = order;
        }

        public string Name { get; protected set; }
        public string Type { get; protected set; }
        public int Order { get; protected set; }
    }
}