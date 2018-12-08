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

        public object[] GetSignatureTopicAsTheOnlyTopic()
        {
            return new object[] {GetSignatureTopic()};
        }

        public object GetSignatureTopic()
        {
            return eventABI.Sha3Signature.EnsureHexPrefix();
        }

        public object[] GetTopics(object[] firstTopic)
        {
            return new[] {GetSignatureTopic(), GetValueTopic(firstTopic, 1)};
        }

        public object[] GetTopics(object[] firstTopic, object[] secondTopic)
        {
            return new[] {GetSignatureTopic(), GetValueTopic(firstTopic, 1), GetValueTopic(secondTopic, 2)};
        }

        public object[] GetTopics(object[] firstTopic, object[] secondTopic, object[] thirdTopic)
        {
            return new[]
            {
                GetSignatureTopic(), GetValueTopic(firstTopic, 1), GetValueTopic(secondTopic, 2),
                GetValueTopic(thirdTopic, 3)
            };
        }

        public object[] GetTopics(object firstTopic)
        {
            return GetTopics(new []{firstTopic});
        }

        public object[] GetTopics(object firstTopic, object secondTopic)
        {
            return GetTopics(new[] { firstTopic }, new[] {secondTopic});
        }

        public object[] GetTopics(object firstTopic, object secondTopic, object thirdTopic)
        {
            return GetTopics(new[] { firstTopic }, new[] { secondTopic }, new[] {thirdTopic});
        }

        public object[] GetTopics<T1>(T1 firstTopic)
        {
            return GetTopics(firstTopic == null ? null : new[] { (object)firstTopic });
        }

        public object[] GetTopics<T1, T2>(T1 firstTopic, T2 secondTopic)
        {
            return GetTopics(firstTopic == null ? null : new[] { (object)firstTopic },
                secondTopic == null ? null : new[] { (object)secondTopic });
                        
        }

        public object[] GetTopics<T1, T2, T3>(T1 firstTopic, T2 secondTopic, T3 thirdTopic)
        {

            return GetTopics(firstTopic == null ? null : new[] { (object)firstTopic },
                secondTopic == null ? null : new[] { (object)secondTopic },
                 thirdTopic == null ? null : new[] { (object)thirdTopic });
        }


        public object[] GetTopics<T1>(T1[] firstOrTopics)
        {
            return GetTopics(firstOrTopics.Cast<object>().ToArray());
        }

        public object[] GetTopics<T1, T2>(T1[] firstOrTopics, T2[] secondOrTopics)
        {
            return GetTopics(firstOrTopics.Cast<object>().ToArray(),  secondOrTopics.Cast<object>().ToArray());
        }

        public object[] GetTopics<T1, T2, T3>(T1[] firstOrTopics, T2[] secondOrTopics, T3[] thirdOrTopics)
        {
            return GetTopics(firstOrTopics.Cast<object>().ToArray(), secondOrTopics.Cast<object>().ToArray(), thirdOrTopics.Cast<object>().ToArray());
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