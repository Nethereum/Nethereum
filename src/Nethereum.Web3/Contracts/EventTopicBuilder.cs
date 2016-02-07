using System;
using System.Linq;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Web3
{
    public class EventTopicBuilder
    {
        private EventABI eventABI;

        public EventTopicBuilder(EventABI eventABI)
        {
            this.eventABI = eventABI;
        }

        public object GetSignaguteTopic()
        {
            return new[] {eventABI.Sha33Signature};
        }

        public object[] GetValueTopic(object[] values, int paramNumber)
        {
            if (values == null) return null;
            var encoded = new object[values.Length];
            var parameter = eventABI.InputParameters.FirstOrDefault(x => x.Order == paramNumber);
            if (parameter == null) throw new Exception("Event parameter not found at " + paramNumber);

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                {
                    encoded[i] = "0x" + parameter.ABIType.Encode(values[i]).ToHex();
                }
               
            }
            return encoded;
        }

    }
}