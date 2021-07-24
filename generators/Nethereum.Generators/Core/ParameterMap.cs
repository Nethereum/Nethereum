using Nethereum.Generators.Model;

namespace Nethereum.Generators.Core
{
    public class ParameterMap<T1, T2> where T1 : Parameter
        where T2 : Parameter
    {
        public ParameterMap(T1 from, T2 to)
        {
            From = from;
            To = to;
        }
        public T1 From { get; set; }
        public T2 To { get; set; }
    }
}