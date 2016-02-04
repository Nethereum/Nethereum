using System;

namespace Nethereum.ABI.FunctionEncoding
{
    public class Parameter
    {

        public Parameter(string type, string name = null, int order = 1)
        {
            this.Name = name;
            this.Type = type;
            this.Order = order;
            this.ABIType = ABIType.CreateABIType(type);
        }

        public Parameter(string type, int order):this(type, null, order)
        {

        }

        public string Name { get; private set; }
        public string Type { get; private set; }
        public ABIType ABIType { get; private set; }
        public int Order { get; private set; }
    }
}