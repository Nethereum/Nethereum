using System.Linq;
using System.Reflection;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.ABI.FunctionEncoding
{
    public class EventTopicDecoder: ParameterDecoder
    {

        public T DecodeTopics<T>(object[] topics, string data) where T : new()
        {
            var type = typeof(T);
            var result = new T();

            var properties = GetPropertiesWithParameterAttributes(type.GetTypeInfo().DeclaredProperties.ToArray());
            var topicNumber = 0;
            foreach (var topic in topics)
            {
                //skip the first one as it is the signature
                if (topicNumber > 0)
                {
                    var property = properties.FirstOrDefault(x => CustomAttributeExtensions.GetCustomAttribute<ParameterAttribute>((MemberInfo) x).Order == topicNumber);
                    result = DecodeAttributes(topic.ToString(), result, property);
                }
                topicNumber = topicNumber + 1;
            }

            var dataProperties = properties.Where(x => x.GetCustomAttribute<ParameterAttribute>().Order >= topicNumber);
            result = DecodeAttributes(data, result, dataProperties.ToArray());
            return result;
        }
       
    }
}