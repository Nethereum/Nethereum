using System;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.RLP
{
    public class RLPStringFormatter
    {
        public static string Format(IRLPElement element)
        {
            var output = new StringBuilder();
            if (element == null)
                throw new Exception("RLPElement object can't be null");
            if (element is RLPCollection rlpCollection)
            {
                output.Append("[");
                foreach (var innerElement in rlpCollection)
                    Format(innerElement);
                output.Append("]");
            }
            else
            {
                output.Append(element.RLPData.ToHex() + ", ");
            }
            return output.ToString();
        }
    }
}