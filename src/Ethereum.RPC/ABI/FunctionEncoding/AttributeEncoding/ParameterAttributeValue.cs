using System.Reflection;
using Ethereum.RPC.ABI.Attributes;

namespace Ethereum.RPC.ABI
{
    public class ParameterAttributeValue
    {
        public ParameterAttribute ParameterAttribute { get; set; }
        public object Value { get; set; }
        public PropertyInfo PropertyInfo { get; set; }
    }
}