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

        private const int AddressLength = 20;

        public Withdrawal Decode(byte[] data)
        {
            var reader = new RlpReader(data);
            var withdrawal = Decode(reader);
            reader.EnsureEnd();
            return withdrawal;
        }

        public Withdrawal Decode(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var withdrawal = new Withdrawal
            {
                Index = reader.ReadUInt64(),
                ValidatorIndex = reader.ReadUInt64(),
                Address = reader.ReadFixed(AddressLength),
                AmountInGwei = reader.ReadUInt64()
            };
            reader.EnsureEnd(scope);
            return withdrawal;
        }
    }
}
