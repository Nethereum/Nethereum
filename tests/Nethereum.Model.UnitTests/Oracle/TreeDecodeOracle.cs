using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.UnitTests.Oracle
{
    public static class TreeDecodeOracle
    {
        public static ISignedTransaction DecodeTransaction(byte[] rlp, bool allowBlobNetworkWrapper)
        {
            RejectBytesOutsideTheTransactionRlp(rlp);
            var transaction = DecodeTransactionBody(rlp, allowBlobNetworkWrapper);
            var nonce = transaction.GetNonce();
            if (nonce > new EvmUInt256(ulong.MaxValue))
                throw new TransactionNonceOverflowException(nonce);
            return transaction;
        }

        private static void RejectBytesOutsideTheTransactionRlp(byte[] rlp)
        {
            var offset = rlp.Length > 0 && rlp.IsTypeTransaction() ? 1 : 0;
            if (rlp.Length <= offset)
                throw new TransactionRlpLengthException(rlp.Length - offset, 0);

            var itemLength = RLP.RLP.GetFirstElementLength(rlp, offset);
            var available = rlp.Length - offset;
            if (itemLength != available)
                throw new TransactionRlpLengthException(available, itemLength);
        }

        private static ISignedTransaction DecodeTransactionBody(byte[] rlp, bool allowBlobNetworkWrapper)
        {
            if (rlp.IsTypeTransaction())
            {
                var type = (TransactionType)rlp[0];
                var body = new byte[rlp.Length - 1];
                Array.Copy(rlp, 1, body, 0, body.Length);
                switch (type)
                {
                    case TransactionType.LegacyEIP2930: return Decode2930(body);
                    case TransactionType.EIP1559: return Decode1559(body);
                    case TransactionType.Blob: return Decode4844(body, allowBlobNetworkWrapper);
                    case TransactionType.EIP7702: return Decode7702(body);
                    default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
                }
            }

            RejectMalformedScalars(rlp);
            var signed = DecodeSigned(rlp, 6);
            var builder = signed.signature == null
                ? new RLPSignedDataHashBuilder(signed.data, 6)
                : new RLPSignedDataHashBuilder(signed.data, signed.signature.R, signed.signature.S, signed.signature.V, 6);
            return builder.IsVSignatureForChain()
                ? (SignedLegacyTransaction)new LegacyTransactionChainId(builder)
                : new LegacyTransaction(builder);
        }

        private static readonly LegacyTransactionField[] ScalarFields =
        {
            LegacyTransactionField.Nonce, LegacyTransactionField.GasPrice,
            LegacyTransactionField.GasLimit, LegacyTransactionField.Value
        };

        private static readonly LegacyTransactionField[] WideFields =
        {
            LegacyTransactionField.Nonce, LegacyTransactionField.Value
        };

        private static void RejectMalformedScalars(byte[] rlp)
        {
            RLPCollection items;
            try { items = RLP.RLP.Decode(rlp, RLP.RLP.UntrustedElementBudget(rlp.Length)) as RLPCollection; }
            catch { return; }
            if (items == null) return;

            foreach (var field in ScalarFields)
            {
                if ((int)field >= items.Count) return;
                if (!RlpScalar.IsCanonical(items[(int)field].RLPData))
                    throw new NonCanonicalScalarRlpException(field);
            }

            foreach (var field in WideFields)
            {
                if ((int)field >= items.Count) return;
                var scalar = items[(int)field].RLPData;
                if (!RlpScalar.FitsA256BitField(scalar))
                    throw new ScalarWiderThanItsFieldException(field, scalar.Length);
            }
        }

        private static (byte[][] data, Signature signature) DecodeSigned(byte[] rawdata, int n)
        {
            var decodedElements = (RLPCollection)RLP.RLP.Decode(rawdata, RLP.RLP.UntrustedElementBudget(rawdata.Length));
            var data = new byte[n][];
            for (var i = 0; i < n; i++) data[i] = decodedElements[i].RLPData;
            var signature = DecodeSignature(decodedElements, n);
            if (signature != null && (signature.R == null || signature.S == null)) signature = null;
            return (data, signature);
        }

        private static Signature DecodeSignature(RLPCollection decodedElements, int n)
        {
            Signature signature = null;
            if (decodedElements.Count > n && decodedElements[n + 1].RLPData != null)
            {
                var v = new byte[] { 0 };
                if (decodedElements[n].RLPData != null) v = decodedElements[n].RLPData;
                signature = new Signature(decodedElements[n + 1].RLPData, decodedElements[n + 2].RLPData, v);
            }
            return signature;
        }

        private static RLPCollection Tree(byte[] body)
        {
            return (RLPCollection)RLP.RLP.Decode(body, RLP.RLP.UntrustedElementBudget(body.Length));
        }

        private static Transaction2930 Decode2930(byte[] body)
        {
            var e = Tree(body);
            return new Transaction2930(
                e[0].RLPData.ToEvmUInt256FromRLPDecoded(), e[1].RLPData.ToEvmUInt256FromRLPDecoded(),
                e[2].RLPData.ToEvmUInt256FromRLPDecoded(), e[3].RLPData.ToEvmUInt256FromRLPDecoded(),
                e[4].RLPData?.ToHex(true), e[5].RLPData.ToEvmUInt256FromRLPDecoded(), e[6].RLPData?.ToHex(true),
                DecodeAccessList(e[7].RLPData), DecodeSignature(e, 8));
        }

        private static Transaction1559 Decode1559(byte[] body)
        {
            var e = Tree(body);
            return new Transaction1559(
                e[0].RLPData.ToEvmUInt256FromRLPDecoded(), e[1].RLPData.ToEvmUInt256FromRLPDecoded(),
                e[2].RLPData.ToEvmUInt256FromRLPDecoded(), e[3].RLPData.ToEvmUInt256FromRLPDecoded(),
                e[4].RLPData.ToEvmUInt256FromRLPDecoded(), e[5].RLPData?.ToHex(true),
                e[6].RLPData.ToEvmUInt256FromRLPDecoded(), e[7].RLPData?.ToHex(true),
                DecodeAccessList(e[8].RLPData), DecodeSignature(e, 9));
        }

        private static Transaction7702 Decode7702(byte[] body)
        {
            var e = Tree(body);
            return new Transaction7702(
                e[0].RLPData.ToEvmUInt256FromRLPDecoded(), e[1].RLPData.ToEvmUInt256FromRLPDecoded(),
                e[2].RLPData.ToEvmUInt256FromRLPDecoded(), e[3].RLPData.ToEvmUInt256FromRLPDecoded(),
                e[4].RLPData.ToEvmUInt256FromRLPDecoded(), e[5].RLPData?.ToHex(true),
                e[6].RLPData.ToEvmUInt256FromRLPDecoded(), e[7].RLPData?.ToHex(true),
                DecodeAccessList(e[8].RLPData), DecodeAuthorisations(e[9].RLPData), DecodeSignature(e, 10));
        }

        private static Transaction4844 Decode4844(byte[] body, bool allowNetworkWrapper)
        {
            var e = Tree(body);
            if ((e.Count == 4 || e.Count == 5) && e[0] is RLPCollection)
            {
                if (!allowNetworkWrapper)
                    throw new BlobSidecarNotAllowedInBlockBodyException();

                var tx = Decode4844Fields((RLPCollection)e[0]);
                var first = e.Count == 5 ? 2 : 1;
                var sidecar = new BlobSidecar(ByteList(e[first]), ByteList(e[first + 1]), ByteList(e[first + 2]));
                if (e.Count == 5)
                    sidecar.WrapperVersion = e[1].RLPData is { Length: > 0 } version ? version[version.Length - 1] : (byte)0;
                tx.Sidecar = sidecar;
                return tx;
            }
            return Decode4844Fields(e);
        }

        private static Transaction4844 Decode4844Fields(RLPCollection e)
        {
            return new Transaction4844(
                e[0].RLPData.ToEvmUInt256FromRLPDecoded(), e[1].RLPData.ToEvmUInt256FromRLPDecoded(),
                e[2].RLPData.ToEvmUInt256FromRLPDecoded(), e[3].RLPData.ToEvmUInt256FromRLPDecoded(),
                e[4].RLPData.ToEvmUInt256FromRLPDecoded(), e[5].RLPData?.ToHex(true),
                e[6].RLPData.ToEvmUInt256FromRLPDecoded(), e[7].RLPData?.ToHex(true),
                DecodeAccessList(e[8].RLPData), e[9].RLPData.ToEvmUInt256FromRLPDecoded(),
                ByteList(e[10]), DecodeSignature(e, 11));
        }

        private static List<byte[]> ByteList(IRLPElement element)
        {
            var result = new List<byte[]>();
            if (element is RLPCollection collection)
                foreach (var item in collection) result.Add(item.RLPData);
            return result;
        }

        private static List<AccessListItem> DecodeAccessList(byte[] encoded)
        {
            if (encoded == null || encoded.Length == 0 || encoded[0] == RLP.RLP.OFFSET_SHORT_LIST) return null;

            var decodedList = (RLPCollection)RLP.RLP.Decode(encoded);
            var accessLists = new List<AccessListItem>();
            foreach (var element in decodedList)
            {
                var item = (RLPCollection)element;
                var accessListItem = new AccessListItem { Address = item[0].RLPData.ToHex() };
                foreach (var key in (RLPCollection)item[1])
                    accessListItem.StorageKeys.Add(key.RLPData);
                accessLists.Add(accessListItem);
            }
            return accessLists;
        }

        private static List<Authorisation7702Signed> DecodeAuthorisations(byte[] encoded)
        {
            var decodedList = (RLPCollection)RLP.RLP.Decode(encoded);
            var result = new List<Authorisation7702Signed>();
            foreach (var element in decodedList)
            {
                if (!(element is RLPCollection item) || item.Count != 6)
                    throw new AuthorisationFormatException("an authorization is a list of exactly 6 fields");

                result.Add(new Authorisation7702Signed
                {
                    ChainId = AuthScalar(item[0], 32, "chain_id").ToEvmUInt256FromRLPDecoded(),
                    Address = AuthAddress(item[1]).ToHex(),
                    Nonce = AuthScalar(item[2], 8, "nonce").ToEvmUInt256FromRLPDecoded(),
                    V = AuthScalar(item[3], 1, "y_parity") ?? new byte[0],
                    R = AuthScalar(item[4], 32, "r") ?? new byte[0],
                    S = AuthScalar(item[5], 32, "s") ?? new byte[0]
                });
            }
            return result;
        }

        private static byte[] AuthScalar(IRLPElement element, int maxBytes, string field)
        {
            if (element is RLPCollection)
                throw new AuthorisationFormatException($"{field} is a list, not a scalar");
            var bytes = element.RLPData;
            if (!RlpScalar.IsCanonical(bytes))
                throw new AuthorisationFormatException($"{field} is encoded with a leading zero byte");
            if (bytes != null && bytes.Length > maxBytes)
                throw new AuthorisationFormatException($"{field} is {bytes.Length} bytes and its field holds {maxBytes}");
            return bytes;
        }

        private static byte[] AuthAddress(IRLPElement element)
        {
            if (element is RLPCollection || element.RLPData == null || element.RLPData.Length != 20)
                throw new AuthorisationFormatException("address is not 20 bytes");
            return element.RLPData;
        }

        public static BlockHeader DecodeHeader(byte[] raw)
        {
            var budgeted = RLP.RLP.Decode(raw, RLP.RLP.UntrustedElementBudget(raw.Length)) as RLPCollection;
            if (budgeted == null)
                throw new ArgumentException("Block header RLP is not a list.", nameof(raw));
            var count = budgeted.Count;
            if (count != 15 && count != 16 && count != 17 && count != 20 && count != 21 && count != 23)
                throw new ArgumentException($"A block header with {count} fields matches no fork this build knows.", nameof(raw));

            var c = (RLPCollection)RLP.RLP.Decode(raw);
            var header = new BlockHeader
            {
                ParentHash = Exact(c[0].RLPData, 32, "parentHash"),
                UnclesHash = Exact(c[1].RLPData, 32, "unclesHash"),
                Coinbase = Exact(c[2].RLPData, 20, "coinbase").ToHex(),
                StateRoot = Exact(c[3].RLPData, 32, "stateRoot"),
                TransactionsHash = Exact(c[4].RLPData, 32, "transactionsRoot"),
                ReceiptHash = Exact(c[5].RLPData, 32, "receiptsRoot"),
                LogsBloom = Exact(c[6].RLPData, 256, "logsBloom"),
                Difficulty = c[7].RLPData.ToEvmUInt256FromRLPDecoded(),
                BlockNumber = c[8].RLPData.ToEvmUInt256FromRLPDecoded(),
                GasLimit = c[9].RLPData.ToLongBitsFromRLPDecoded(),
                GasUsed = c[10].RLPData.ToLongBitsFromRLPDecoded(),
                Timestamp = c[11].RLPData.ToLongBitsFromRLPDecoded(),
                ExtraData = c[12].RLPData,
                MixHash = Exact(c[13].RLPData, 32, "mixHash"),
                Nonce = Exact(c[14].RLPData, 8, "nonce"),
            };
            if (count >= 16) header.BaseFee = c[15].RLPData.ToEvmUInt256FromRLPDecoded();
            if (count >= 17) header.WithdrawalsRoot = Exact(c[16].RLPData, 32, "withdrawalsRoot");
            if (count >= 20)
            {
                header.BlobGasUsed = unchecked((long)c[17].RLPData.ToULongFromRLPDecoded());
                header.ExcessBlobGas = unchecked((long)c[18].RLPData.ToULongFromRLPDecoded());
                header.ParentBeaconBlockRoot = Exact(c[19].RLPData, 32, "parentBeaconBlockRoot");
            }
            if (count >= 21) header.RequestsHash = Exact(c[20].RLPData, 32, "requestsHash");
            if (count >= 23)
            {
                header.BlockAccessListHash = Exact(c[21].RLPData, 32, "blockAccessListHash");
                header.SlotNumber = c[22].RLPData.ToULongFromRLPDecoded();
            }
            return header;
        }

        private static byte[] Exact(byte[] value, int width, string field)
        {
            if (value == null || value.Length != width)
                throw new InvalidOperationException($"RLP_INVALID_FIELD_WIDTH: header field '{field}' must be {width} bytes.");
            return value;
        }
    }
}
