using System;
using System.Linq;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Web3
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
            return new[] {Ensure0XPrefix(eventABI.Sha33Signature)};
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

        private string Ensure0XPrefix(string input)
        {
            return !input.StartsWith("0x") ? "0x" + input : input;
        }

        public object[] GetValueTopic(object[] values, int paramNumber)
        {
            if (values == null) return null;
            var encoded = new object[values.Length];
            var parameter = eventABI.InputParameters.FirstOrDefault(x => x.Order == paramNumber);
            if (parameter == null) throw new Exception("Event parameter not found at " + paramNumber);

            for (var i = 0; i < values.Length; i++)
                if (values[i] != null)
                    encoded[i] = Ensure0XPrefix(parameter.ABIType.Encode(values[i]).ToHex());
            return encoded;
        }
    }
}