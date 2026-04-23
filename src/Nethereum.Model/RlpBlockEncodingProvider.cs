using System;
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

        public byte[] EncodeTransaction(ISignedTransaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            return transaction.GetRLPEncoded();
        }

        public Receipt DecodeReceipt(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return ReceiptEncoder.Current.Decode(data);
        }

        public BlockHeader DecodeBlockHeader(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return BlockHeaderEncoder.Current.Decode(data);
        }

        public Account DecodeAccount(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return AccountEncoder.Current.Decode(data);
        }

        public Log DecodeLog(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return LogEncoder.Current.Decode(data);
        }

        public ISignedTransaction DecodeTransaction(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return TransactionFactory.CreateTransaction(data);
        }
    }
}
