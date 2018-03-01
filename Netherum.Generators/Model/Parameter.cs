namespace Nethereum.ABI.Model
{
    public class Parameter
    {
        public Parameter(string type, string name = null, int order = 1, string serpentSignature = null)
        {
            Name = name;
            Type = type;
            Order = order;
            SerpentSignature = serpentSignature;
        }

        public Parameter(string type, int order) : this(type, null, order)
        {
        }

        public string Name { get; private set; }
        public string Type { get; private set; }
        public int Order { get; private set; }
        public bool Indexed { get; set; }
        public string SerpentSignature { get; private set; }
    }
}