using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model
{
    internal static class TransactionRlpDecoder
    {
        private const int AddressBytes = 20;
        private const int HashBytes = 32;
        private const int KzgBytes = 48;
        private const byte CellProofSidecarVersion = 1;

        public static ISignedTransaction Decode(byte[] rlp, bool allowBlobNetworkWrapper, bool ownsBuffer, bool requireSignature = false)
        {
            var offset = rlp.Length > 0 && rlp.IsTypeTransaction() ? 1 : 0;
            RejectBytesOutsideTheTransactionRlp(rlp, offset);

            ISignedTransaction transaction;
            if (offset == 1)
            {
                var typed = DecodeBody(rlp, (TransactionType)rlp[0], allowBlobNetworkWrapper, requireSignature);
                typed.OriginalRlpEncoded = ownsBuffer ? rlp : DefensiveCopyOf(rlp);
                transaction = typed;
            }
            else
            {
                transaction = DecodeLegacy(rlp);
            }

            RejectANonceWiderThan64Bits(transaction);
            return transaction;
        }

        public static SignedTypeTransaction DecodeBody(byte[] data, TransactionType type, bool allowBlobNetworkWrapper, bool requireSignature = false)
        {
            var offset = data.Length > 0 && data[0] == type.AsByte() ? 1 : 0;
            var reader = new RlpReader(data, offset, data.Length - offset);
            SignedTypeTransaction transaction;
            switch (type)
            {
                case TransactionType.LegacyEIP2930:
                    transaction = Read2930(reader, requireSignature);
                    break;
                case TransactionType.EIP1559:
                    transaction = Read1559(reader, requireSignature);
                    break;
                case TransactionType.Blob:
                    transaction = Read4844(reader, allowBlobNetworkWrapper, requireSignature);
                    break;
                case TransactionType.EIP7702:
                    transaction = Read7702(reader, requireSignature);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            reader.EnsureEnd();
            return transaction;
        }

        private static void RejectBytesOutsideTheTransactionRlp(byte[] rlp, int offset)
        {
            if (rlp.Length <= offset)
                throw new TransactionRlpLengthException(rlp.Length - offset, 0);

            var itemLength = RLP.RLP.GetFirstElementLength(rlp, offset);
            var available = rlp.Length - offset;
            if (itemLength != available)
                throw new TransactionRlpLengthException(available, itemLength);
        }

        private static readonly EvmUInt256 MaxTransactionNonce = new EvmUInt256(ulong.MaxValue);

        private static void RejectANonceWiderThan64Bits(ISignedTransaction transaction)
        {
            var nonce = transaction.GetNonce();
            if (nonce > MaxTransactionNonce)
                throw new TransactionNonceOverflowException(nonce);
        }

        private static byte[] DefensiveCopyOf(byte[] rlp)
        {
            var copy = new byte[rlp.Length];
            Array.Copy(rlp, copy, rlp.Length);
            return copy;
        }

        private static ISignedTransaction DecodeLegacy(byte[] rlp)
        {
            var fields = RLPSignedDataDecoder.ReadFields(rlp, SignedLegacyTransaction.NUMBER_ENCODING_ELEMENTS);
            LegacyTransactionRlp.RejectMalformedScalars(fields);
            if (fields.Signed && fields.LegacySignature() == null)
                throw new TransactionFieldFormatException(LegacyTransactionField.SignatureR, "empty");

            var builder = new RLPSignedDataHashBuilder(rlp, fields.Data, fields.LegacySignature(),
                SignedLegacyTransaction.NUMBER_ENCODING_ELEMENTS);
            return builder.IsVSignatureForChain()
                ? (SignedLegacyTransaction)new LegacyTransactionChainId(builder)
                : new LegacyTransaction(builder);
        }

        private static Transaction2930 Read2930(RlpReader reader, bool requireSignature)
        {
            var scope = reader.ReadListHeader();
            var chainId = Scalar(reader, LegacyTransactionField.ChainId);
            var nonce = Scalar(reader, LegacyTransactionField.Nonce);
            var gasPrice = Scalar(reader, LegacyTransactionField.GasPrice);
            var gasLimit = GasLimit(reader);
            var receiver = Receiver(reader);
            var amount = Scalar(reader, LegacyTransactionField.Value);
            var data = HexOrNull(reader);
            var accessList = AccessListRLPEncoderDecoder.Read(reader);
            var signature = Signature(reader, scope, requireSignature);
            reader.EnsureEnd(scope);
            return new Transaction2930(chainId, nonce, gasPrice, gasLimit, receiver, amount, data, accessList, signature);
        }

        private static Transaction1559 Read1559(RlpReader reader, bool requireSignature)
        {
            var scope = reader.ReadListHeader();
            var chainId = Scalar(reader, LegacyTransactionField.ChainId);
            var nonce = Scalar(reader, LegacyTransactionField.Nonce);
            var maxPriorityFeePerGas = Scalar(reader, LegacyTransactionField.MaxPriorityFeePerGas);
            var maxFeePerGas = Scalar(reader, LegacyTransactionField.MaxFeePerGas);
            var gasLimit = GasLimit(reader);
            var receiver = Receiver(reader);
            var amount = Scalar(reader, LegacyTransactionField.Value);
            var data = HexOrNull(reader);
            var accessList = AccessListRLPEncoderDecoder.Read(reader);
            var signature = Signature(reader, scope, requireSignature);
            reader.EnsureEnd(scope);
            return new Transaction1559(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, receiver, amount,
                data, accessList, signature);
        }

        private static Transaction7702 Read7702(RlpReader reader, bool requireSignature)
        {
            var scope = reader.ReadListHeader();
            var chainId = Scalar(reader, LegacyTransactionField.ChainId);
            var nonce = Scalar(reader, LegacyTransactionField.Nonce);
            var maxPriorityFeePerGas = Scalar(reader, LegacyTransactionField.MaxPriorityFeePerGas);
            var maxFeePerGas = Scalar(reader, LegacyTransactionField.MaxFeePerGas);
            var gasLimit = GasLimit(reader);
            var receiver = RequiredReceiver(reader);
            var amount = Scalar(reader, LegacyTransactionField.Value);
            var data = HexOrNull(reader);
            var accessList = AccessListRLPEncoderDecoder.Read(reader);
            var authorisations = AuthorisationListRLPEncoderDecoder.Read(reader);
            var signature = Signature(reader, scope, requireSignature);
            reader.EnsureEnd(scope);
            return new Transaction7702(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, receiver, amount,
                data, accessList, authorisations, signature);
        }

        private static Transaction4844 Read4844(RlpReader reader, bool allowBlobNetworkWrapper, bool requireSignature)
        {
            var scope = reader.ReadListHeader();
            var count = reader.CountItems(scope);
            if ((count == 4 || count == 5) && reader.PeekKind() == RlpKind.List)
            {
                if (!allowBlobNetworkWrapper)
                    throw new BlobSidecarNotAllowedInBlockBodyException();

                var inner = reader.ReadListHeader();
                var transaction = Read4844Fields(reader, inner, requireSignature);
                reader.EnsureEnd(inner);

                var wrapperVersion = count == 5 ? WrapperVersion(reader) : (byte)0;
                var blobs = FixedStrings(reader, BlobEncoder.BLOB_SIZE);
                var commitments = FixedStrings(reader, KzgBytes);
                var proofs = FixedStrings(reader, KzgBytes);
                reader.EnsureEnd(scope);
                transaction.Sidecar = new BlobSidecar(blobs, commitments, proofs) { WrapperVersion = wrapperVersion };
                return transaction;
            }

            var plain = Read4844Fields(reader, scope, requireSignature);
            reader.EnsureEnd(scope);
            return plain;
        }

        private static Transaction4844 Read4844Fields(RlpReader reader, RlpListScope scope, bool requireSignature)
        {
            var chainId = Scalar(reader, LegacyTransactionField.ChainId);
            var nonce = Scalar(reader, LegacyTransactionField.Nonce);
            var maxPriorityFeePerGas = Scalar(reader, LegacyTransactionField.MaxPriorityFeePerGas);
            var maxFeePerGas = Scalar(reader, LegacyTransactionField.MaxFeePerGas);
            var gasLimit = GasLimit(reader);
            var receiver = RequiredReceiver(reader);
            var amount = Scalar(reader, LegacyTransactionField.Value);
            var data = HexOrNull(reader);
            var accessList = AccessListRLPEncoderDecoder.Read(reader);
            var maxFeePerBlobGas = Scalar(reader, LegacyTransactionField.MaxFeePerBlobGas);
            var blobVersionedHashes = BlobVersionedHashes(reader);
            var signature = Signature(reader, scope, requireSignature);
            return new Transaction4844(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, receiver, amount,
                data, accessList, maxFeePerBlobGas, blobVersionedHashes, signature);
        }

        private static byte WrapperVersion(RlpReader reader)
        {
            var version = reader.ReadUInt64();
            if (version != CellProofSidecarVersion)
                throw new InvalidOperationException("RLP blob sidecar version " + version + " is not supported");
            return (byte)version;
        }

        private static List<byte[]> FixedStrings(RlpReader reader, int length)
        {
            var scope = reader.ReadListHeader();
            var items = new List<byte[]>();
            while (!reader.IsAtEnd(scope))
                items.Add(reader.ReadFixed(length));
            reader.EnsureEnd(scope);
            return items;
        }

        private static List<byte[]> BlobVersionedHashes(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var hashes = new List<byte[]>(scope.Length / (HashBytes + 1));
            while (!reader.IsAtEnd(scope))
                hashes.Add(reader.ReadFixed(HashBytes));
            reader.EnsureEnd(scope);
            return hashes;
        }

        private static EvmUInt256 Scalar(RlpReader reader, LegacyTransactionField field)
        {
            var bytes = reader.ReadBytes();
            LegacyTransactionRlp.RejectMalformedScalar(bytes, field);
            return bytes.ToEvmUInt256FromRLPDecoded();
        }

        private static EvmUInt256 GasLimit(RlpReader reader)
        {
            var bytes = reader.ReadBytes();
            LegacyTransactionRlp.RejectMalformedScalar(bytes, LegacyTransactionField.GasLimit);
            LegacyTransactionRlp.RejectWiderThan64Bits(bytes, LegacyTransactionField.GasLimit);
            return bytes.ToEvmUInt256FromRLPDecoded();
        }

        private static string RequiredReceiver(RlpReader reader)
        {
            var receiver = Receiver(reader);
            if (receiver == null)
                throw new TransactionFieldFormatException(LegacyTransactionField.ReceiveAddress, "empty where a 20-byte address is required");
            return receiver;
        }

        private static string Receiver(RlpReader reader)
        {
            if (reader.PeekKind() == RlpKind.List)
                throw new TransactionFieldFormatException(LegacyTransactionField.ReceiveAddress, "a list");
            var size = reader.PeekSize();
            if (size != 0 && size != AddressBytes)
                throw new TransactionFieldFormatException(LegacyTransactionField.ReceiveAddress, size + " bytes");
            var bytes = reader.ReadBytes();
            return bytes.Length == 0 ? null : bytes.ToHex(true);
        }

        private static string HexOrNull(RlpReader reader)
        {
            var bytes = reader.ReadBytes();
            return bytes.Length == 0 ? null : bytes.ToHex(true);
        }

        private static Signature Signature(RlpReader reader, RlpListScope scope, bool requireSignature)
        {
            if (reader.IsAtEnd(scope))
            {
                if (requireSignature)
                    throw new InvalidOperationException("A typed transaction from a peer carries its signature");
                return null;
            }
            var v = reader.ReadBytes();
            var r = reader.ReadBytes();
            var s = reader.ReadBytes();
            LegacyTransactionRlp.RejectMalformedScalar(v, LegacyTransactionField.SignatureV);
            LegacyTransactionRlp.RejectMalformedScalar(r, LegacyTransactionField.SignatureR);
            LegacyTransactionRlp.RejectMalformedScalar(s, LegacyTransactionField.SignatureS);
            return RLPSignedDataDecoder.SignatureOrNull(v, r, s);
        }
    }
}
