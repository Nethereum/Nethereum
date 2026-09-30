using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Documentation;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Freezer
{
    public sealed class ReceiptFieldDeriver
    {
        private readonly ITransactionVerificationAndRecovery _signer;
        private readonly IBlobBaseFeeFractionResolver _blobFraction;

        public ReceiptFieldDeriver(ITransactionVerificationAndRecovery signer, IBlobBaseFeeFractionResolver blobFraction)
        {
            _signer = signer ?? throw new ArgumentNullException(nameof(signer));
            _blobFraction = blobFraction ?? throw new ArgumentNullException(nameof(blobFraction));
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "ReceiptFieldDeriver.Derive — rebuild gasUsed/bloom/logs/txHash on read")]
        public IReadOnlyList<DerivedReceipt> Derive(
            BlockHeader header,
            BlockBodyCluster body,
            IReadOnlyList<ReceiptForStorage> stored)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (stored == null) throw new ArgumentNullException(nameof(stored));
            if (stored.Count != body.Txs.Count)
                throw new InvalidOperationException(
                    $"receipt/transaction count mismatch: {stored.Count} receipts vs {body.Txs.Count} transactions");

            var blockHash = ComputeBlockHash(header);
            var blockNumber = header.BlockNumber.ToLong();
            var baseFee = header.BaseFee ?? EvmUInt256.Zero;

            var derived = new List<DerivedReceipt>(stored.Count);
            var previousCumulativeGasUsed = BigInteger.Zero;
            var blockLogIndex = 0;

            for (var i = 0; i < stored.Count; i++)
            {
                var tx = body.Txs[i];
                var receipt = stored[i];

                var gasUsed = DeriveGasUsed(receipt.CumulativeGasUsed, previousCumulativeGasUsed);
                previousCumulativeGasUsed = receipt.CumulativeGasUsed;

                var logs = DeriveLogFields(receipt.Logs, blockHash, blockNumber, tx.Hash, i, blockLogIndex);
                blockLogIndex += logs.Count;

                var bloom = DeriveBloom(receipt.Logs);
                var effectiveGasPrice = DeriveEffectiveGasPrice(tx, baseFee);
                var contractAddress = DeriveContractAddress(tx);
                var (blobGasUsed, blobGasPrice) = DeriveBlobFields(tx, header);

                derived.Add(new DerivedReceipt(
                    receipt.PostStateOrStatus,
                    receipt.CumulativeGasUsed,
                    gasUsed,
                    bloom,
                    logs,
                    tx.Hash,
                    tx.TransactionType,
                    effectiveGasPrice,
                    contractAddress,
                    blobGasUsed,
                    blobGasPrice));
            }

            return derived;
        }

        private static BigInteger DeriveGasUsed(BigInteger cumulativeGasUsed, BigInteger previousCumulativeGasUsed) =>
            cumulativeGasUsed - previousCumulativeGasUsed;

        private static byte[] DeriveBloom(IReadOnlyList<Log> logs)
        {
            var bloom = new LogBloomFilter();
            foreach (var log in logs)
                bloom.AddLog(log);
            return bloom.Data;
        }

        private static IReadOnlyList<DerivedLog> DeriveLogFields(
            IReadOnlyList<Log> logs, byte[] blockHash, long blockNumber, byte[] txHash, int txIndex, int startingLogIndex)
        {
            var result = new List<DerivedLog>(logs.Count);
            for (var j = 0; j < logs.Count; j++)
                result.Add(new DerivedLog(logs[j], blockHash, blockNumber, txHash, txIndex, startingLogIndex + j));
            return result;
        }

        private static EvmUInt256 DeriveEffectiveGasPrice(ISignedTransaction tx, EvmUInt256 baseFee) =>
            tx.GetEffectiveGasPrice(baseFee);

        private string DeriveContractAddress(ISignedTransaction tx)
        {
            if (!tx.IsContractCreation())
                return null;

            var sender = _signer.GetSenderAddress(tx);
            var nonce = tx.GetNonce();
            return ContractUtils.CalculateContractAddress(sender, nonce.ToLong());
        }

        private (BigInteger? blobGasUsed, EvmUInt256? blobGasPrice) DeriveBlobFields(ISignedTransaction tx, BlockHeader header)
        {
            if (tx.TransactionType != TransactionType.Blob || !(tx is Transaction4844 blobTx))
                return (null, null);

            var blobCount = blobTx.BlobVersionedHashes?.Count ?? 0;
            var blobGasUsed = (BigInteger)BlobGasCalculator.CalculateTotalBlobGas(blobCount);

            var excessBlobGas = new EvmUInt256(header.ExcessBlobGas ?? 0L);
            var fraction = _blobFraction.FractionForBlock(header);
            var blobGasPrice = BlobGasCalculator.CalculateBlobBaseFee(excessBlobGas, fraction);

            return (blobGasUsed, blobGasPrice);
        }

        private static byte[] ComputeBlockHash(BlockHeader header)
        {
            var encoded = BlockHeaderEncoder.Current.Encode(header, legacyMode: false);
            return new Sha3Keccack().CalculateHash(encoded);
        }
    }
}
