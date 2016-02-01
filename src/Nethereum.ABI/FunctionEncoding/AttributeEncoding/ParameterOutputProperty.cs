using System.Reflection;

namespace Nethereum.ABI.FunctionEncoding.AttributeEncoding
{
    public class ParameterOutputProperty : ParameterOutput
    {
        public PropertyInfo PropertyInfo { get; set; }
    }
}