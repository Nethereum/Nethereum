using Nethereum.Util;

namespace Nethereum.Model
{
    public class RlpBlockEncodingProvider : IBlockEncodingProvider
    {
        public static RlpBlockEncodingProvider Instance { get; } = new RlpBlockEncodingProvider();

        public byte[] EncodeReceipt(Receipt receipt) => ReceiptEncoder.Current.Encode(receipt);
        public byte[] EncodeBlockHeader(BlockHeader header) => BlockHeaderEncoder.Current.Encode(header);
        public byte[] EncodeAccount(Account account) => AccountEncoder.Current.Encode(account);
        public byte[] EncodeLog(Log log) => LogEncoder.Current.Encode(log);

        public byte[] EncodeWithdrawal(ulong index, ulong validatorIndex, byte[] address, ulong amountInGwei)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(new EvmUInt256(index).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(new EvmUInt256(validatorIndex).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(address),
                RLP.RLP.EncodeElement(new EvmUInt256(amountInGwei).ToBytesForRLPEncoding())
            );
        }
    }
}
