using Nethereum.ABI.FunctionEncoding.Attributes;
using System;
using System.Linq;
using System.Reflection;

namespace Nethereum.Contracts
{
    internal class TopicFilterContainer<T> where T: class
    {
        internal TopicFilterContainer()
        {
            var indexedParameters = PropertiesExtractor
                .GetPropertiesWithParameterAttribute(typeof(T))
                .Select(p => new TopicFilter(p, p.GetCustomAttribute<ParameterAttribute>(true)))
                .Where(p => p.ParameterAttribute?.Parameter.Indexed ?? false)
                .OrderBy(p => p.ParameterAttribute.Order)
                .ToArray();

            Empty = indexedParameters.Length == 0;

            Topic1 = indexedParameters.Length > 0 ? indexedParameters[0] : TopicFilter.Empty;
            Topic2 = indexedParameters.Length > 1 ? indexedParameters[1] : TopicFilter.Empty;
            Topic3 = indexedParameters.Length > 2 ? indexedParameters[2] : TopicFilter.Empty;

            Topics = new []{Topic1, Topic2, Topic3};
        }

        public bool Empty { get; private set; }

        public TopicFilter Topic1 { get; private set; }
        public TopicFilter Topic2 { get; private set; }
        public TopicFilter Topic3 { get; private set; }

        private TopicFilter[] Topics { get; set; }

        public TopicFilter GetTopic(PropertyInfo pInfo)
        {
            return Topics
                       .FirstOrDefault(t => t.EventDtoProperty.Name == pInfo.Name) ?? 
                   throw new ArgumentException($"Property '{pInfo.Name}' does not represent a topic. The property must have a ParameterAttribute which is flagged as indexed");;
        }
        
    }
}