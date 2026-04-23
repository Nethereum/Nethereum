using System;
using System.Collections.Generic;

namespace Nethereum.Model.SSZ
{
    /// <summary>
    /// SSZ implementation of <see cref="IBlockEncodingProvider"/>. Each method
    /// delegates to the matching Ssz*Encoder singleton.
    ///
    /// Coverage:
    ///   EncodeBlockHeader → SszBlockHeaderEncoder.Current.Encode (EIP-7807)
    ///   EncodeLog         → SszLogEncoder.Current.Encode (EIP-6466)
    ///   EncodeReceipt     → dispatches to SszReceiptEncoder.EncodeBasicReceipt /
    ///                       EncodeCreateReceipt / SetCode per EIP-6466 variant
    ///                       rules. Requires Receipt.From (and optionally
    ///                       ContractAddress / Authorities) to be populated by the
    ///                       block producer at tx-execution time.
    ///   EncodeWithdrawal  → SszWithdrawalEncoder.Current.Encode (EIP-6465)
    ///   EncodeAccount     → throws NotImplementedException. Binary-trie AppChains
    ///                       persist accounts as EIP-7864 Basic Data Leaves (32-byte
    ///                       packed), not as SSZ-serialised Account records. No SSZ
    ///                       Account encoder exists in EIP-7864 / EIP-6466 / EIP-7807.
    /// </summary>
    public class SszBlockEncodingProvider : IBlockEncodingProvider
    {
        public static SszBlockEncodingProvider Instance { get; } = new SszBlockEncodingProvider();

        public byte[] EncodeBlockHeader(BlockHeader header)
            => SszBlockHeaderEncoder.Current.Encode(header);

        public byte[] EncodeLog(Log log)
            => SszLogEncoder.Current.Encode(log);

        public byte[] EncodeReceipt(Receipt receipt)
        {
            if (receipt == null)
                throw new ArgumentNullException(nameof(receipt));

            // EIP-6466 requires `from` on every receipt variant. Producers populating
            // Receipt objects for SSZ-mode chains must set Receipt.From at tx-execution
            // time. Raising a clear error here surfaces incorrectly-populated receipts
            // immediately rather than producing garbage output.
            if (string.IsNullOrEmpty(receipt.From))
                throw new InvalidOperationException(
                    "SSZ EncodeReceipt: Receipt.From is required (EIP-6466). " +
                    "The block producer must populate it at tx-execution time. " +
                    "See docs/superpowers/plans/2026-04-20-appchain-config-surface-A-plan.md.");

            var status = receipt.HasSucceeded == true;
            var gasUsed = (ulong)receipt.CumulativeGasUsed.ToLong();

            var encoder = SszReceiptEncoder.Current;

            // Variant selection per EIP-6466:
            //   SetCodeReceipt for EIP-7702 set-code transactions (authorities present)
            //   CreateReceipt  for contract-deployment transactions (contract_address present)
            //   BasicReceipt   otherwise
            byte[] payload;
            byte selector;
            if (receipt.Authorities != null && receipt.Authorities.Count > 0)
            {
                payload = encoder.EncodeSetCodeReceipt(
                    receipt.From, gasUsed, receipt.Logs, status, receipt.Authorities);
                selector = SszReceiptEncoder.SelectorSetCodeReceipt;
            }
            else if (!string.IsNullOrEmpty(receipt.ContractAddress))
            {
                payload = encoder.EncodeCreateReceipt(
                    receipt.From, gasUsed, receipt.ContractAddress, receipt.Logs, status);
                selector = SszReceiptEncoder.SelectorCreateReceipt;
            }
            else
            {
                payload = encoder.EncodeBasicReceipt(
                    receipt.From, gasUsed, receipt.Logs, status);
                selector = SszReceiptEncoder.SelectorBasicReceipt;
            }

            return encoder.EncodeReceipt(selector, payload);
        }

        public byte[] EncodeWithdrawal(ulong index, ulong validatorIndex, byte[] address, ulong amountInGwei)
            => SszWithdrawalEncoder.Current.Encode(index, validatorIndex, address, amountInGwei);

        public byte[] EncodeTransaction(ISignedTransaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            var encoder = SszTransactionEncoder.Current;
            byte selector;
            byte[] payload;

            switch (transaction)
            {
                case Transaction4844 blob:
                    selector = SszTransactionEncoder.SelectorRlpBlob;
                    payload = encoder.EncodeTransaction4844Payload(blob);
                    break;
                case Transaction7702 setCode:
                    selector = SszTransactionEncoder.SelectorRlpSetCode;
                    payload = encoder.EncodeTransaction7702Payload(setCode);
                    break;
                case Transaction1559 eip1559:
                    selector = string.IsNullOrEmpty(eip1559.ReceiverAddress)
                        ? SszTransactionEncoder.SelectorRlpCreate
                        : SszTransactionEncoder.SelectorRlpBasic;
                    payload = encoder.EncodeTransaction1559Payload(eip1559);
                    break;
                default:
                    throw new NotImplementedException(
                        $"SSZ EncodeTransaction not yet implemented for {transaction.GetType().Name} " +
                        $"(TransactionType={transaction.TransactionType}). EIP-6404 defines 10 " +
                        "selectors (0x01-0x0a); currently only 1559 (0x07/0x08), 4844 (0x09) " +
                        "and 7702 (0x0a) are wired in SszTransactionEncoder.");
            }

            var signature = transaction.Signature;
            var signatureBytes = signature != null
                ? SszTransactionEncoder.PackSignatureBytes(signature.R, signature.S, signature.V)
                : Array.Empty<byte>();

            return encoder.EncodeTransaction(selector, payload, signatureBytes);
        }

        public byte[] EncodeAccount(Account account)
        {
            // EIP-7864 binary-trie chains persist account state as the 32-byte packed
            // Basic Data Leaf (version + code_size + nonce + balance) plus a separate
            // code-hash slot. There is no SSZ Account container defined in any of the
            // in-scope EIPs (6466, 7807, 7864). Callers that need account bytes for
            // SSZ-mode persistence should go through the binary-trie leaf-packing
            // utilities in Nethereum.Merkle.Binary.Keys, not through this provider.
            throw new NotImplementedException(
                "SSZ EncodeAccount is not defined. Binary-trie AppChains pack accounts " +
                "per EIP-7864 (Basic Data Leaf) — there is no SSZ Account encoder in " +
                "EIP-6466 / 7807 / 7864. Use Nethereum.Merkle.Binary.Keys.BasicDataLeaf " +
                "for packing.");
        }

        // --- Decode ---

        public Receipt DecodeReceipt(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            var encoder = SszReceiptEncoder.Current;
            var span = (ReadOnlySpan<byte>)data;
            var selector = encoder.DecodeReceiptSelector(span);
            var payload = encoder.DecodeReceiptData(span);

            string from;
            ulong gasUsed;
            List<Log> logs;
            bool status;
            string contractAddress = null;
            List<string> authorities = null;

            switch (selector)
            {
                case SszReceiptEncoder.SelectorBasicReceipt:
                    encoder.DecodeBasicReceipt(payload, out from, out gasUsed, out logs, out status);
                    break;
                case SszReceiptEncoder.SelectorCreateReceipt:
                    encoder.DecodeCreateReceipt(payload, out from, out gasUsed, out contractAddress, out logs, out status);
                    break;
                case SszReceiptEncoder.SelectorSetCodeReceipt:
                    encoder.DecodeSetCodeReceipt(payload, out from, out gasUsed, out logs, out status, out authorities);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"SSZ DecodeReceipt: unknown selector 0x{selector:x2}. " +
                        "EIP-6466 defines 0x01 (Basic), 0x02 (Create), 0x03 (SetCode).");
            }

            return new Receipt
            {
                PostStateOrStatus = status ? new byte[] { 1 } : Array.Empty<byte>(),
                CumulativeGasUsed = new Nethereum.Util.EvmUInt256(gasUsed),
                Logs = logs ?? new List<Log>(),
                From = from,
                ContractAddress = contractAddress,
                Authorities = authorities
            };
        }

        public BlockHeader DecodeBlockHeader(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return SszBlockHeaderEncoder.Current.Decode(data);
        }

        public Account DecodeAccount(byte[] data)
        {
            // See EncodeAccount above — EIP-7864 packed leaves, not SSZ Account records.
            throw new NotImplementedException(
                "SSZ DecodeAccount is not defined. Binary-trie AppChains unpack " +
                "accounts per EIP-7864 (Basic Data Leaf). Use " +
                "Nethereum.Merkle.Binary.Keys.BasicDataLeaf for unpacking.");
        }

        public Log DecodeLog(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return SszLogEncoder.Current.Decode(data);
        }

        public ISignedTransaction DecodeTransaction(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            var encoder = SszTransactionEncoder.Current;
            encoder.ParseTransaction(data, out var selector, out var payload, out var sigBytes);
            var signature = SszTransactionEncoder.UnpackSignature(sigBytes);

            switch (selector)
            {
                case SszTransactionEncoder.SelectorRlpBasic:
                    return encoder.DecodeTransaction1559Payload(payload, isCreate: false, signature);
                case SszTransactionEncoder.SelectorRlpCreate:
                    return encoder.DecodeTransaction1559Payload(payload, isCreate: true, signature);
                case SszTransactionEncoder.SelectorRlpBlob:
                    return encoder.DecodeTransaction4844Payload(payload, signature);
                case SszTransactionEncoder.SelectorRlpSetCode:
                    return encoder.DecodeTransaction7702Payload(payload, signature);
                default:
                    throw new NotImplementedException(
                        $"SSZ DecodeTransaction not implemented for selector 0x{selector:x2}. " +
                        "EIP-6404 defines 10 selectors (0x01-0x0a); currently 0x07/0x08 (1559), " +
                        "0x09 (4844 blob) and 0x0a (7702) are wired.");
            }
        }
    }
}
