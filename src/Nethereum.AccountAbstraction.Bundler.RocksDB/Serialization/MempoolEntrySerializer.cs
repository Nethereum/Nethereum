using System.Numerics;
using System.Text;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using BigInteger = System.Numerics.BigInteger;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization
{
    public static class MempoolEntrySerializer
    {
        public static byte[] Serialize(MempoolEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            var userOpBytes = SerializePackedUserOperation(entry.UserOperation);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.UserOpHash ?? "")),
                RLP.RLP.EncodeElement(userOpBytes),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.EntryPoint ?? "")),
                RLP.RLP.EncodeElement(entry.SubmittedAt.ToUnixTimeMilliseconds().ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((int)entry.State).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.TransactionHash ?? "")),
                RLP.RLP.EncodeElement(entry.BlockNumber?.ToBytesForRLPEncoding() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.Error ?? "")),
                RLP.RLP.EncodeElement(entry.RetryCount.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(entry.Prefund.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.Aggregator ?? "")),
                RLP.RLP.EncodeElement(entry.ValidUntil.HasValue ? ((BigInteger)entry.ValidUntil.Value).ToBytesForRLPEncoding() : Array.Empty<byte>()),
                RLP.RLP.EncodeElement(entry.ValidAfter.HasValue ? ((BigInteger)entry.ValidAfter.Value).ToBytesForRLPEncoding() : Array.Empty<byte>()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.Factory ?? "")),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.Paymaster ?? "")),
                RLP.RLP.EncodeElement(entry.Priority.ToBytesForRLPEncoding())
            );
        }

        public static MempoolEntry? Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            var decoded = RLP.RLP.Decode(data);
            var elements = (RLPCollection)decoded;

            var userOpBytes = elements[1].RLPData;
            var userOp = DeserializePackedUserOperation(userOpBytes);

            return new MempoolEntry
            {
                UserOpHash = Encoding.UTF8.GetString(elements[0].RLPData ?? Array.Empty<byte>()),
                UserOperation = userOp,
                EntryPoint = Encoding.UTF8.GetString(elements[2].RLPData ?? Array.Empty<byte>()),
                SubmittedAt = DateTimeOffset.FromUnixTimeMilliseconds(elements[3].RLPData.ToLongFromRLPDecoded()),
                State = (MempoolEntryState)(int)elements[4].RLPData.ToLongFromRLPDecoded(),
                TransactionHash = GetNullableString(elements[5].RLPData),
                BlockNumber = elements[6].RLPData?.Length > 0 ? elements[6].RLPData.ToBigIntegerFromRLPDecoded() : null,
                Error = GetNullableString(elements[7].RLPData),
                RetryCount = (int)elements[8].RLPData.ToLongFromRLPDecoded(),
                Prefund = elements[9].RLPData.ToBigIntegerFromRLPDecoded(),
                Aggregator = GetNullableString(elements[10].RLPData),
                ValidUntil = elements[11].RLPData?.Length > 0 ? (ulong)elements[11].RLPData.ToLongFromRLPDecoded() : null,
                ValidAfter = elements[12].RLPData?.Length > 0 ? (ulong)elements[12].RLPData.ToLongFromRLPDecoded() : null,
                Factory = GetNullableString(elements[13].RLPData),
                Paymaster = GetNullableString(elements[14].RLPData),
                Priority = elements[15].RLPData.ToBigIntegerFromRLPDecoded()
            };
        }

        private static byte[] SerializePackedUserOperation(PackedUserOperation op)
        {
            if (op == null) return Array.Empty<byte>();

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(op.Sender?.HexToByteArray() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.Nonce.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(op.InitCode ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.CallData ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.AccountGasLimits ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.PreVerificationGas.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(op.GasFees ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.PaymasterAndData ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.Signature ?? Array.Empty<byte>())
            );
        }

        private static PackedUserOperation DeserializePackedUserOperation(byte[]? data)
        {
            if (data == null || data.Length == 0) return new PackedUserOperation();

            var decoded = RLP.RLP.Decode(data);
            var elements = (RLPCollection)decoded;

            return new PackedUserOperation
            {
                Sender = elements[0].RLPData?.Length > 0 ? elements[0].RLPData.ToHex(true) : null,
                Nonce = elements[1].RLPData.ToBigIntegerFromRLPDecoded(),
                InitCode = elements[2].RLPData,
                CallData = elements[3].RLPData,
                AccountGasLimits = elements[4].RLPData,
                PreVerificationGas = elements[5].RLPData.ToBigIntegerFromRLPDecoded(),
                GasFees = elements[6].RLPData,
                PaymasterAndData = elements[7].RLPData,
                Signature = elements[8].RLPData
            };
        }

        private static string? GetNullableString(byte[]? data)
        {
            if (data == null || data.Length == 0) return null;
            var str = Encoding.UTF8.GetString(data);
            return string.IsNullOrEmpty(str) ? null : str;
        }

        public static byte[] CreateSenderKey(string sender, BigInteger nonce)
        {
            var senderBytes = (sender?.ToLowerInvariant() ?? "").HexToByteArray();
            var nonceBytes = nonce.ToBytesForRLPEncoding();
            var paddedNonce = new byte[32];
            if (nonceBytes.Length <= 32)
            {
                Buffer.BlockCopy(nonceBytes, 0, paddedNonce, 32 - nonceBytes.Length, nonceBytes.Length);
            }

            var result = new byte[senderBytes.Length + paddedNonce.Length];
            Buffer.BlockCopy(senderBytes, 0, result, 0, senderBytes.Length);
            Buffer.BlockCopy(paddedNonce, 0, result, senderBytes.Length, paddedNonce.Length);
            return result;
        }

        public static byte[] CreateSenderPrefixKey(string sender)
        {
            return (sender?.ToLowerInvariant() ?? "").HexToByteArray();
        }

        public static byte[] StringToKey(string value)
        {
            return Encoding.UTF8.GetBytes(value?.ToLowerInvariant() ?? "");
        }
    }
}
