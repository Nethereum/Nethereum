using System;
using System.Linq;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Contracts
{
    public class EventTopicBuilder
    {
        private readonly EventABI eventABI;

        public EventTopicBuilder(EventABI eventABI)
        {
            this.eventABI = eventABI;
        }

        public object GetSignaguteTopic()
        {
            return new[] {EnsureHexPrefix(eventABI.Sha33Signature)};
        }

        public object[] GetTopics(object[] firstTopic)
        {
            return new[] {GetSignaguteTopic(), GetValueTopic(firstTopic, 1)};
        }

        public object[] GetTopics(object[] firstTopic, object[] secondTopic)
        {
            return new[] {GetSignaguteTopic(), GetValueTopic(firstTopic, 1), GetValueTopic(secondTopic, 2)};
        }

        public object[] GetTopics(object[] firstTopic, object[] secondTopic, object[] thirdTopic)
        {
            return new[]
            {
                GetSignaguteTopic(), GetValueTopic(firstTopic, 1), GetValueTopic(secondTopic, 2),
                GetValueTopic(thirdTopic, 3)
            };
        }

        public object[] GetValueTopic(object[] values, int paramNumber)
        {
            if (values == null) return null;
            var encoded = new object[values.Length];
            var parameter = eventABI.InputParameters.FirstOrDefault(x => x.Order == paramNumber);
            if (parameter == null) throw new Exception("Event parameter not found at " + paramNumber);

            for (var i = 0; i < values.Length; i++)
                if (values[i] != null)
                    encoded[i] = EnsureHexPrefix(parameter.ABIType.Encode(values[i]).ToHex());
            return encoded;
        }

        private string EnsureHexPrefix(string input)
        {
            return input.EnsureHexPrefix();
        }
    }
}