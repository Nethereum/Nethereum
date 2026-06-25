using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class Withdrawal
    {
        public ulong Index { get; set; }
        public ulong ValidatorIndex { get; set; }
        public byte[] Address { get; set; }
        public ulong AmountInGwei { get; set; }
    }

    public class WithdrawalEncoder
    {
        public static WithdrawalEncoder Current { get; } = new();

        public byte[] Encode(Withdrawal w)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(new EvmUInt256(w.Index).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(new EvmUInt256(w.ValidatorIndex).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(w.Address),
                RLP.RLP.EncodeElement(new EvmUInt256(w.AmountInGwei).ToBytesForRLPEncoding())
            );
        }

        public Withdrawal Decode(byte[] data)
        {
            var items = (RLPCollection)RLP.RLP.Decode(data);
            return new Withdrawal
            {
                Index = (ulong)items[0].RLPData.ToLongFromRLPDecoded(),
                ValidatorIndex = (ulong)items[1].RLPData.ToLongFromRLPDecoded(),
                Address = items[2].RLPData,
                AmountInGwei = (ulong)items[3].RLPData.ToLongFromRLPDecoded()
            };
        }
    }
}
