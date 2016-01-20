using System.Reflection;

namespace Ethereum.RPC.ABI
{
    public class ParameterOutputProperty : ParameterOutput
    {
        public PropertyInfo PropertyInfo { get; set; }
    }
}