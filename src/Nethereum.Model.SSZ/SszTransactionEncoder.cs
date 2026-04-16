using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Ssz;
using Nethereum.Util;

namespace Nethereum.Model.SSZ
{
    public class SszTransactionEncoder
    {
        public const byte SelectorRlpLegacyReplayableBasic = 0x01;
        public const byte SelectorRlpLegacyReplayableCreate = 0x02;
        public const byte SelectorRlpLegacyBasic = 0x03;
        public const byte SelectorRlpLegacyCreate = 0x04;
        public const byte SelectorRlpAccessListBasic = 0x05;
        public const byte SelectorRlpAccessListCreate = 0x06;
        public const byte SelectorRlpBasic = 0x07;
        public const byte SelectorRlpCreate = 0x08;
        public const byte SelectorRlpBlob = 0x09;
        public const byte SelectorRlpSetCode = 0x0a;

        public const byte Secp256k1Algorithm = 0xFF;
        public const int AddressLength = 20;

        private static readonly bool[] BasicFeesActiveFields = { true };
        private static readonly bool[] BlobFeesActiveFields = { true, true };

        public static readonly SszTransactionEncoder Current = new SszTransactionEncoder();

        // ================================================================
        // Encode Transaction1559 payload as SSZ
        // ================================================================
        // RlpBasicTransactionPayload (selector 0x07): active_fields=[1,1,1,1,1,1,1,1,1,1]
        //   type_(uint8)=fixed, chain_id(uint256)=fixed, nonce(uint64)=fixed,
        //   max_fees_per_gas(BasicFeesPerGas)=variable, gas(uint64)=fixed,
        //   to(address)=fixed, value(uint256)=fixed,
        //   input_(ProgressiveByteList)=variable, access_list(ProgressiveList)=variable,
        //   max_priority_fees_per_gas(BasicFeesPerGas)=variable

        public byte[] EncodeTransaction1559Payload(Transaction1559 tx)
        {
            var isCreate = string.IsNullOrEmpty(tx.ReceiverAddress);
            var inputBytes = tx.Data?.HexToByteArray() ?? Array.Empty<byte>();
            var accessListEncoded = SszAccessListEncoder.Current.EncodeAccessList(tx.AccessList);
            var maxFeeEncoded = EncodeBasicFees(tx.MaxFeePerGas);
            var maxPriorityFeeEncoded = EncodeBasicFees(tx.MaxPriorityFeePerGas);

            // Fixed section size calculation
            // type(1) + chain_id(32) + nonce(8) + offset_maxFees(4) + gas(8) +
            // [to(20) if not create] + value(32) + offset_input(4) + offset_accessList(4) +
            // offset_maxPriorityFees(4)
            var fixedSize = 1 + 32 + 8 + 4 + 8 + (isCreate ? 0 : 20) + 32 + 4 + 4 + 4;

            // Variable offsets (relative to start of container)
            var maxFeesOffset = (uint)fixedSize;
            var inputOffset = maxFeesOffset + (uint)maxFeeEncoded.Length;
            var accessListOffset = inputOffset + 4 + (uint)inputBytes.Length; // +4 for input length prefix
            var maxPriorityFeesOffset = accessListOffset + (uint)accessListEncoded.Length;

            using var writer = new SszWriter();
            // Fixed fields
            writer.WriteBytes(new[] { TransactionType.EIP1559.AsByte() }); // type_
            writer.WriteFixedBytes(tx.ChainId.ToLittleEndian(), 32); // chain_id
            writer.WriteUInt64((ulong)(tx.Nonce ?? EvmUInt256.Zero)); // nonce
            writer.WriteUInt32(maxFeesOffset); // offset: max_fees_per_gas
            writer.WriteUInt64((ulong)(tx.GasLimit ?? EvmUInt256.Zero)); // gas
            if (!isCreate)
                writer.WriteFixedBytes(tx.ReceiverAddress.HexToByteArray(), AddressLength); // to
            writer.WriteFixedBytes((tx.Amount ?? EvmUInt256.Zero).ToLittleEndian(), 32); // value
            writer.WriteUInt32(inputOffset); // offset: input_
            writer.WriteUInt32(accessListOffset); // offset: access_list
            writer.WriteUInt32(maxPriorityFeesOffset); // offset: max_priority_fees_per_gas

            // Variable fields (in offset order)
            writer.WriteBytes(maxFeeEncoded); // max_fees_per_gas
            writer.WriteUInt32((uint)inputBytes.Length); // input_ length prefix
            writer.WriteBytes(inputBytes); // input_ data
            writer.WriteBytes(accessListEncoded); // access_list
            writer.WriteBytes(maxPriorityFeeEncoded); // max_priority_fees_per_gas

            return writer.ToArray();
        }

        // ================================================================
        // Decode Transaction1559 payload from SSZ
        // ================================================================

        public Transaction1559 DecodeTransaction1559Payload(ReadOnlySpan<byte> data, bool isCreate,
            ISignature signature = null)
        {
            var reader = new SszReader(data);

            var typeByte = reader.ReadFixedBytes(1)[0]; // type_
            var chainIdBytes = reader.ReadFixedBytes(32);
            var chainId = EvmUInt256.FromLittleEndian(chainIdBytes);
            var nonce = reader.ReadUInt64();
            var maxFeesOffset = reader.ReadUInt32();
            var gas = reader.ReadUInt64();

            string receiverAddress = null;
            if (!isCreate)
            {
                var toBytes = reader.ReadFixedBytes(AddressLength);
                receiverAddress = "0x" + toBytes.ToHex();
            }

            var valueBytes = reader.ReadFixedBytes(32);
            var value = EvmUInt256.FromLittleEndian(valueBytes);
            var inputOffset = reader.ReadUInt32();
            var accessListOffset = reader.ReadUInt32();
            var maxPriorityFeesOffset = reader.ReadUInt32();

            // Decode variable fields from their offsets
            var maxFeeData = data.Slice((int)maxFeesOffset, (int)(inputOffset - maxFeesOffset));
            var maxFee = DecodeBasicFees(maxFeeData);

            // input_: length-prefixed bytes
            var inputLengthSpan = data.Slice((int)inputOffset, 4);
            var inputLength = BinaryPrimitives.ReadUInt32LittleEndian(inputLengthSpan);
            var inputBytes = data.Slice((int)inputOffset + 4, (int)inputLength);
            var inputHex = inputBytes.Length > 0 ? "0x" + inputBytes.ToArray().ToHex() : null;

            var accessListData = data.Slice((int)accessListOffset,
                (int)(maxPriorityFeesOffset - accessListOffset));
            var accessList = SszAccessListEncoder.Current.DecodeAccessList(accessListData);

            var maxPriorityFeeData = data.Slice((int)maxPriorityFeesOffset);
            var maxPriorityFee = DecodeBasicFees(maxPriorityFeeData);

            if (signature != null)
            {
                return new Transaction1559(chainId, nonce, maxPriorityFee, maxFee, gas,
                    receiverAddress, value, inputHex, accessList,
                    new Signature(signature.R, signature.S, signature.V));
            }

            return new Transaction1559(chainId, nonce, maxPriorityFee, maxFee, gas,
                receiverAddress, value, inputHex, accessList);
        }

        // ================================================================
        // Encode Transaction7702 payload as SSZ
        // ================================================================
        // RlpSetCodeTransactionPayload (selector 0x0a): active_fields=[1,1,1,1,1,1,1,1,1,1,0,1]
        // Fields: type_(uint8), chain_id(uint256), nonce(uint64),
        //   max_fees_per_gas(BasicFeesPerGas), gas(uint64), to(address), value(uint256),
        //   input_(ProgressiveByteList), access_list(ProgressiveList),
        //   max_priority_fees_per_gas(BasicFeesPerGas), authorization_list(ProgressiveList)

        public byte[] EncodeTransaction7702Payload(Transaction7702 tx)
        {
            var inputBytes = tx.Data?.HexToByteArray() ?? Array.Empty<byte>();
            var accessListEncoded = SszAccessListEncoder.Current.EncodeAccessList(tx.AccessList);
            var maxFeeEncoded = EncodeBasicFees(tx.MaxFeePerGas);
            var maxPriorityFeeEncoded = EncodeBasicFees(tx.MaxPriorityFeePerGas);
            var authListEncoded = EncodeAuthorisationList(tx.AuthorisationList);

            // Fixed: type(1) + chain_id(32) + nonce(8) + offset_maxFees(4) + gas(8) +
            //        to(20) + value(32) + offset_input(4) + offset_accessList(4) +
            //        offset_maxPriorityFees(4) + offset_authList(4)
            var fixedSize = 1 + 32 + 8 + 4 + 8 + 20 + 32 + 4 + 4 + 4 + 4;

            var maxFeesOffset = (uint)fixedSize;
            var inputOffset = maxFeesOffset + (uint)maxFeeEncoded.Length;
            var accessListOffset = inputOffset + 4 + (uint)inputBytes.Length;
            var maxPriorityFeesOffset = accessListOffset + (uint)accessListEncoded.Length;
            var authListOffset = maxPriorityFeesOffset + (uint)maxPriorityFeeEncoded.Length;

            using var writer = new SszWriter();
            writer.WriteBytes(new[] { TransactionType.EIP7702.AsByte() });
            writer.WriteFixedBytes(tx.ChainId.ToLittleEndian(), 32);
            writer.WriteUInt64((ulong)(tx.Nonce ?? EvmUInt256.Zero));
            writer.WriteUInt32(maxFeesOffset);
            writer.WriteUInt64((ulong)(tx.GasLimit ?? EvmUInt256.Zero));
            writer.WriteFixedBytes(tx.ReceiverAddress.HexToByteArray(), AddressLength);
            writer.WriteFixedBytes((tx.Amount ?? EvmUInt256.Zero).ToLittleEndian(), 32);
            writer.WriteUInt32(inputOffset);
            writer.WriteUInt32(accessListOffset);
            writer.WriteUInt32(maxPriorityFeesOffset);
            writer.WriteUInt32(authListOffset);

            writer.WriteBytes(maxFeeEncoded);
            writer.WriteUInt32((uint)inputBytes.Length);
            writer.WriteBytes(inputBytes);
            writer.WriteBytes(accessListEncoded);
            writer.WriteBytes(maxPriorityFeeEncoded);
            writer.WriteBytes(authListEncoded);

            return writer.ToArray();
        }

        public Transaction7702 DecodeTransaction7702Payload(ReadOnlySpan<byte> data, ISignature signature = null)
        {
            var reader = new SszReader(data);

            var typeByte = reader.ReadFixedBytes(1)[0];
            var chainId = EvmUInt256.FromLittleEndian(reader.ReadFixedBytes(32));
            var nonce = reader.ReadUInt64();
            var maxFeesOffset = reader.ReadUInt32();
            var gas = reader.ReadUInt64();
            var toBytes = reader.ReadFixedBytes(AddressLength);
            var receiverAddress = "0x" + toBytes.ToHex();
            var value = EvmUInt256.FromLittleEndian(reader.ReadFixedBytes(32));
            var inputOffset = reader.ReadUInt32();
            var accessListOffset = reader.ReadUInt32();
            var maxPriorityFeesOffset = reader.ReadUInt32();
            var authListOffset = reader.ReadUInt32();

            var maxFee = DecodeBasicFees(data.Slice((int)maxFeesOffset, (int)(inputOffset - maxFeesOffset)));

            var inputLengthSpan = data.Slice((int)inputOffset, 4);
            var inputLength = BinaryPrimitives.ReadUInt32LittleEndian(inputLengthSpan);
            var inputBytes = data.Slice((int)inputOffset + 4, (int)inputLength);
            var inputHex = inputBytes.Length > 0 ? "0x" + inputBytes.ToArray().ToHex() : null;

            var accessList = SszAccessListEncoder.Current.DecodeAccessList(
                data.Slice((int)accessListOffset, (int)(maxPriorityFeesOffset - accessListOffset)));
            var maxPriorityFee = DecodeBasicFees(
                data.Slice((int)maxPriorityFeesOffset, (int)(authListOffset - maxPriorityFeesOffset)));
            var authList = DecodeAuthorisationList(data.Slice((int)authListOffset));

            if (signature != null)
            {
                return new Transaction7702(chainId, nonce, maxPriorityFee, maxFee, gas,
                    receiverAddress, value, inputHex, accessList, authList,
                    new Signature(signature.R, signature.S, signature.V));
            }
            return new Transaction7702(chainId, nonce, maxPriorityFee, maxFee, gas,
                receiverAddress, value, inputHex, accessList, authList);
        }

        // ================================================================
        // Authorisation list encode/decode
        // ================================================================

        public byte[] EncodeAuthorisationList(List<Authorisation7702Signed> auths)
        {
            if (auths == null || auths.Count == 0)
                return Array.Empty<byte>();

            var encoded = new List<byte[]>();
            foreach (var auth in auths)
                encoded.Add(EncodeAuthorisation7702(auth));

            var offsetsSize = auths.Count * 4;
            using var writer = new SszWriter();
            var currentOffset = (uint)offsetsSize;
            foreach (var item in encoded)
            {
                writer.WriteUInt32(currentOffset);
                currentOffset += (uint)item.Length;
            }
            foreach (var item in encoded)
                writer.WriteBytes(item);
            return writer.ToArray();
        }

        public byte[] EncodeAuthorisation7702(Authorisation7702Signed auth)
        {
            using var writer = new SszWriter();
            writer.WriteFixedBytes(auth.ChainId.ToLittleEndian(), 32);
            writer.WriteFixedBytes(auth.Address.HexToByteArray(), AddressLength);
            writer.WriteUInt64((ulong)auth.Nonce);
            // Signature: v(1) + r(32) + s(32)
            writer.WriteBytes(auth.V ?? new byte[] { 0 });
            writer.WriteFixedBytes(auth.R ?? new byte[32], 32);
            writer.WriteFixedBytes(auth.S ?? new byte[32], 32);
            return writer.ToArray();
        }

        public List<Authorisation7702Signed> DecodeAuthorisationList(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
                return new List<Authorisation7702Signed>();

            var reader = new SszReader(data);
            var firstOffset = reader.ReadUInt32();
            var count = (int)firstOffset / 4;
            var offsets = new uint[count];
            offsets[0] = firstOffset;
            for (var i = 1; i < count; i++)
                offsets[i] = reader.ReadUInt32();

            var result = new List<Authorisation7702Signed>(count);
            for (var i = 0; i < count; i++)
            {
                var start = (int)offsets[i];
                var end = i + 1 < count ? (int)offsets[i + 1] : data.Length;
                result.Add(DecodeAuthorisation7702(data.Slice(start, end - start)));
            }
            return result;
        }

        public Authorisation7702Signed DecodeAuthorisation7702(ReadOnlySpan<byte> data)
        {
            var reader = new SszReader(data);
            var chainId = EvmUInt256.FromLittleEndian(reader.ReadFixedBytes(32));
            var address = "0x" + reader.ReadFixedBytes(AddressLength).ToHex();
            var nonce = reader.ReadUInt64();
            var v = reader.ReadFixedBytes(1);
            var r = reader.ReadFixedBytes(32);
            var s = reader.ReadFixedBytes(32);
            return new Authorisation7702Signed(chainId, address, nonce, r, s, v);
        }

        // ================================================================
        // Encode/Decode full Transaction (union payload + signature)
        // ================================================================

        public byte[] EncodeTransaction(byte selector, byte[] payloadData, byte[] signatureBytes)
        {
            // CompatibleUnion: [selector][payload_data]
            // Then wrapped in Container { payload, signature }
            // For wire format: [selector][payload_data][signature_length(4)][signature_data]
            using var writer = new SszWriter();
            writer.WriteBytes(new[] { selector });
            writer.WriteBytes(payloadData);
            writer.WriteUInt32((uint)(signatureBytes?.Length ?? 0));
            if (signatureBytes != null && signatureBytes.Length > 0)
                writer.WriteBytes(signatureBytes);
            return writer.ToArray();
        }

        // ================================================================
        // Fee structure encode/decode
        // ================================================================

        public byte[] EncodeBasicFees(EvmUInt256? regularFee)
        {
            return (regularFee ?? EvmUInt256.Zero).ToLittleEndian(); // BasicFeesPerGas has single fixed uint256 field
        }

        public EvmUInt256 DecodeBasicFees(ReadOnlySpan<byte> data)
        {
            if (data.Length < 32) return EvmUInt256.Zero;
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return EvmUInt256.FromLittleEndian(data.Slice(0, 32));
#else
            return EvmUInt256.FromLittleEndian(data.Slice(0, 32).ToArray());
#endif
        }

        // ================================================================
        // HashTreeRoot methods
        // ================================================================

        public byte[] HashTreeRootBasicFees(EvmUInt256? regularFee)
        {
            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootUint256(regularFee)
            };
            return SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, BasicFeesActiveFields);
        }

        public byte[] HashTreeRootBlobFees(EvmUInt256? regularFee, EvmUInt256? blobFee)
        {
            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootUint256(regularFee),
                SszHashTreeRootHelper.HashTreeRootUint256(blobFee)
            };
            return SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, BlobFeesActiveFields);
        }

        public byte[] HashTreeRootTransaction1559(Transaction1559 tx)
        {
            bool isCreate = string.IsNullOrEmpty(tx.ReceiverAddress);
            var activeFields = isCreate
                ? new[] { true, true, true, true, true, false, true, true, true, true }
                : new[] { true, true, true, true, true, true, true, true, true, true };

            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootUint8(TransactionType.EIP1559.AsByte()),
                SszHashTreeRootHelper.HashTreeRootUint256(tx.ChainId),
                SszHashTreeRootHelper.HashTreeRootUint64(tx.Nonce),
                HashTreeRootBasicFees(tx.MaxFeePerGas),
                SszHashTreeRootHelper.HashTreeRootUint64(tx.GasLimit),
            };

            if (!isCreate)
                fieldRoots.Add(SszHashTreeRootHelper.HashTreeRootAddress(tx.ReceiverAddress));

            fieldRoots.Add(SszHashTreeRootHelper.HashTreeRootUint256(tx.Amount));
            fieldRoots.Add(SszHashTreeRootHelper.HashTreeRootProgressiveByteList(tx.Data?.HexToByteArray()));
            fieldRoots.Add(SszAccessListEncoder.Current.HashTreeRootAccessList(tx.AccessList));
            fieldRoots.Add(HashTreeRootBasicFees(tx.MaxPriorityFeePerGas));

            var payloadRoot = SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, activeFields);
            var selector = isCreate ? SelectorRlpCreate : SelectorRlpBasic;
            return HashTreeRootTransactionContainer(payloadRoot, selector, tx.Signature);
        }

        public byte[] HashTreeRootTransaction7702(Transaction7702 tx)
        {
            var activeFields = new[] { true, true, true, true, true, true, true, true, true, true, false, true };

            var authRoots = new List<byte[]>();
            if (tx.AuthorisationList != null)
                foreach (var auth in tx.AuthorisationList)
                    authRoots.Add(HashTreeRootAuthorisation7702(auth));

            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootUint8(TransactionType.EIP7702.AsByte()),
                SszHashTreeRootHelper.HashTreeRootUint256(tx.ChainId),
                SszHashTreeRootHelper.HashTreeRootUint64(tx.Nonce),
                HashTreeRootBasicFees(tx.MaxFeePerGas),
                SszHashTreeRootHelper.HashTreeRootUint64(tx.GasLimit),
                SszHashTreeRootHelper.HashTreeRootAddress(tx.ReceiverAddress),
                SszHashTreeRootHelper.HashTreeRootUint256(tx.Amount),
                SszHashTreeRootHelper.HashTreeRootProgressiveByteList(tx.Data?.HexToByteArray()),
                SszAccessListEncoder.Current.HashTreeRootAccessList(tx.AccessList),
                HashTreeRootBasicFees(tx.MaxPriorityFeePerGas),
                SszMerkleizer.HashTreeRootProgressiveList(authRoots)
            };

            var payloadRoot = SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, activeFields);
            return HashTreeRootTransactionContainer(payloadRoot, SelectorRlpSetCode, tx.Signature);
        }

        public byte[] HashTreeRootAuthorisation7702(Authorisation7702Signed auth)
        {
            bool hasChainId = !auth.ChainId.IsZero;
            var activeFields = hasChainId
                ? new[] { true, true, true, true }
                : new[] { true, false, true, true };

            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootUint8(0x05)
            };
            if (hasChainId)
                fieldRoots.Add(SszHashTreeRootHelper.HashTreeRootUint256(auth.ChainId));
            fieldRoots.Add(SszHashTreeRootHelper.HashTreeRootAddress(auth.Address));
            fieldRoots.Add(SszHashTreeRootHelper.HashTreeRootUint64((ulong)auth.Nonce));

            var payloadRoot = SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, activeFields);
            var selector = hasChainId ? (byte)0x02 : (byte)0x01;
            var unionRoot = SszMerkleizer.HashTreeRootCompatibleUnion(payloadRoot, selector);

            var sigBytes = PackSignatureBytes(auth.R, auth.S, auth.V);
            var sigRoot = SszHashTreeRootHelper.HashTreeRootProgressiveByteList(sigBytes);
            return SszMerkleizer.Merkleize(new List<byte[]> { unionRoot, sigRoot });
        }

        public byte[] HashTreeRootTransactionContainer(byte[] payloadRoot, byte selector, ISignature signature)
        {
            var unionRoot = SszMerkleizer.HashTreeRootCompatibleUnion(payloadRoot, selector);
            var sigBytes = signature != null
                ? PackSignatureBytes(signature.R, signature.S, signature.V)
                : Array.Empty<byte>();
            var sigRoot = SszHashTreeRootHelper.HashTreeRootProgressiveByteList(sigBytes);
            return SszMerkleizer.Merkleize(new List<byte[]> { unionRoot, sigRoot });
        }

        public byte[] HashTreeRootTransactionsRoot(List<byte[]> transactionRoots)
        {
            return SszMerkleizer.HashTreeRootProgressiveList(transactionRoots);
        }

        // ================================================================
        // Helpers
        // ================================================================

        public static byte[] PackSignatureBytes(byte[] r, byte[] s, byte[] v)
        {
            if (r == null && s == null && v == null) return Array.Empty<byte>();

            var result = new List<byte> { Secp256k1Algorithm };
            if (r != null)
            {
                var rPadded = new byte[32];
                Buffer.BlockCopy(r, 0, rPadded, 32 - r.Length, r.Length);
                result.AddRange(rPadded);
            }
            if (s != null)
            {
                var sPadded = new byte[32];
                Buffer.BlockCopy(s, 0, sPadded, 32 - s.Length, s.Length);
                result.AddRange(sPadded);
            }
            if (v != null)
                result.AddRange(v);

            return result.ToArray();
        }

    }
}
