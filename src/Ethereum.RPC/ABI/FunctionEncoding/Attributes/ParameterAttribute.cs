using System;

namespace Ethereum.RPC.ABI.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ParameterAttribute : Attribute
    {
        public Parameter Parameter { get;  }
        public ParameterAttribute(string type, string name = null, int order = 1)
        {
            Parameter = new Parameter(type, name, order);
            
        }

        public ParameterAttribute(string type, int order):this(type, null, order)
        {

        }

        public int Order => Parameter.Order;
        public string Name => Parameter.Name;
        public string Type => Parameter.Type;
    }
}