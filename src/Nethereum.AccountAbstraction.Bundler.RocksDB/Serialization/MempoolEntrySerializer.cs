using System.Numerics;
using System.Text;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;
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
                RLP.RLP.EncodeElement(entry.Priority.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.BlockHash ?? "")),
                // Trailing element (index 17): the EIP-7702 authorisation side-channel. Empty when
                // unset; a count guard on read keeps rows written before this element deserializing.
                RLP.RLP.EncodeElement(SerializeAuthorisation(entry.Eip7702Auth))
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
                Priority = elements[15].RLPData.ToBigIntegerFromRLPDecoded(),
                BlockHash = elements.Count > 16 ? GetNullableString(elements[16].RLPData) : null,
                // Count guard: rows written before the EIP-7702 element (index 17) leave it absent,
                // so an older payload deserializes with Eip7702Auth null.
                Eip7702Auth = elements.Count > 17 ? DeserializeAuthorisation(elements[17].RLPData) : null
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

        /// <summary>
        /// Encodes the EIP-7702 authorisation tuple's six fields (chainId, address, nonce,
        /// yParity, r, s) as a nested RLP list, or an empty element when the tuple is null so the
        /// trailing slot stays present in the fixed entry list.
        /// </summary>
        private static byte[] SerializeAuthorisation(Authorisation? auth)
        {
            if (auth == null) return Array.Empty<byte>();

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(auth.ChainId?.Value.ToBytesForRLPEncoding() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(auth.Address ?? "")),
                RLP.RLP.EncodeElement(auth.Nonce?.Value.ToBytesForRLPEncoding() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(auth.YParity ?? "")),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(auth.R ?? "")),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(auth.S ?? ""))
            );
        }

        private static Authorisation? DeserializeAuthorisation(byte[]? data)
        {
            // The whole-auth empty-element sentinel is the ONLY "no auth" signal. Within a present
            // auth, every field is always meaningful: for the numeric fields, empty bytes mean the
            // value 0 (BigInteger 0 encodes to empty), not absent - so ChainId=0 (EIP-7702's
            // "valid on any chain" sentinel) and Nonce=0 (a fresh EOA's first delegation) survive
            // a reload rather than round-tripping to null.
            if (data == null || data.Length == 0) return null;

            var decoded = RLP.RLP.Decode(data);
            var elements = (RLPCollection)decoded;

            return new Authorisation
            {
                ChainId = new HexBigInteger(elements[0].RLPData.ToBigIntegerFromRLPDecoded()),
                Address = GetNullableString(elements[1].RLPData),
                Nonce = new HexBigInteger(elements[2].RLPData.ToBigIntegerFromRLPDecoded()),
                YParity = GetNullableString(elements[3].RLPData),
                R = GetNullableString(elements[4].RLPData),
                S = GetNullableString(elements[5].RLPData)
            };
        }

        private static string? GetNullableString(byte[]? data)
        {
            if (data == null || data.Length == 0) return null;
            var str = Encoding.UTF8.GetString(data);
            return string.IsNullOrEmpty(str) ? null : str;
        }

        /// <summary>
        /// Sender-index key: sender ‖ entryPoint ‖ nonce. One row per (sender, entryPoint,
        /// nonce) pointing at the occupying operation's hash key — the admission lookup for
        /// the ERC-4337 one-pending-op-per-nonce rule. Sender first so sender-prefix scans
        /// keep working.
        /// </summary>
        public static byte[] CreateSenderKey(string sender, string entryPoint, BigInteger nonce)
        {
            var senderBytes = (sender?.ToLowerInvariant() ?? "").HexToByteArray();
            var entryPointBytes = (entryPoint?.ToLowerInvariant() ?? "").HexToByteArray();
            var nonceBytes = nonce.ToBytesForRLPEncoding();
            var paddedNonce = new byte[32];
            if (nonceBytes.Length <= 32)
            {
                Buffer.BlockCopy(nonceBytes, 0, paddedNonce, 32 - nonceBytes.Length, nonceBytes.Length);
            }

            var result = new byte[senderBytes.Length + entryPointBytes.Length + paddedNonce.Length];
            Buffer.BlockCopy(senderBytes, 0, result, 0, senderBytes.Length);
            Buffer.BlockCopy(entryPointBytes, 0, result, senderBytes.Length, entryPointBytes.Length);
            Buffer.BlockCopy(paddedNonce, 0, result, senderBytes.Length + entryPointBytes.Length, paddedNonce.Length);
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
