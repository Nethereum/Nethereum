using System.Collections.Generic;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Core
{
    public class MessageMap<MFrom, MTo, PFrom, PTo>
        where PFrom : Parameter
        where PTo : Parameter
        where MFrom : IMessage<PFrom>
        where MTo : IMessage<PTo>
    {
        public MessageMap(MFrom from, MTo to, List<ParameterMap<PFrom, PTo>> parameterMaps)
        {
            From = from;
            To = to;
            ParameterMaps = parameterMaps;
        }

        public MFrom From { get; set; }
        public MTo To { get; set; }
        public List<ParameterMap<PFrom, PTo>> ParameterMaps { get; set; }
    }
}