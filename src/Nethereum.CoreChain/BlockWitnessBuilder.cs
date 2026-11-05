using System.Linq;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain
{
    internal static class BlockWitnessBuilder
    {
        internal static byte[]? BuildBlockWitness(
            BlockExecutionOptions options,
            WitnessRecordingStateReader? witnessRecorder,
            IReadOnlyList<TxEntry> txs,
            BlockHeader header,
            ChainConfig chainConfig,
            HardforkName fork,
            BlobParameters? blobParameters,
            IList<WithdrawalEntry>? withdrawals,
            byte[]? parentBeaconBlockRoot,
            byte[]? preStateRoot)
        {
            if (!(options.CaptureWitness && witnessRecorder != null))
                return null;

            var witnessTxs = new List<BlockWitnessTransaction>(txs.Count);
            foreach (var entry in txs)
            {
                witnessTxs.Add(new BlockWitnessTransaction
                {
                    From = entry.CachedSender ?? "",
                    RlpEncoded = entry.Tx.GetRLPEncoded(),

                    AuthorisationAuthorities = entry.Tx.RecoverAuthorities()
                });
            }

            var witnessData = new BlockWitnessData
            {
                BlockNumber = (long)header.BlockNumber,
                Timestamp = header.Timestamp,
                BaseFee = (long)(header.BaseFee ?? 0),
                BlockGasLimit = header.GasLimit,
                ChainId = (long)chainConfig.ChainId,
                Coinbase = header.Coinbase,
                Difficulty = header.MixHash ?? new byte[32],
                PreStateRoot = preStateRoot,
                ParentHash = header.ParentHash ?? new byte[32],
                ExtraData = header.ExtraData ?? Array.Empty<byte>(),
                MixHash = header.MixHash ?? new byte[32],
                Nonce = header.Nonce ?? new byte[8],
                ComputePostStateRoot = true,

                ParentBeaconBlockRoot = parentBeaconBlockRoot,
                BlobGasUsed = header.BlobGasUsed.HasValue ? (long)header.BlobGasUsed.Value : (long?)null,
                ExcessBlobGas = header.ExcessBlobGas.HasValue ? (long)header.ExcessBlobGas.Value : (long?)null,
                RequestsHash = header.RequestsHash,
                SlotNumber = header.SlotNumber,
                Withdrawals = ToWitnessWithdrawals(withdrawals),

                Features = new BlockFeatureConfig
                {
                    Fork = fork,
                    Blob = blobParameters
                },
                Transactions = witnessTxs,
                Accounts = witnessRecorder.GetWitnessAccounts()
            };

            return BinaryBlockWitness.Serialize(witnessData);
        }

        private static List<BlockWithdrawal>? ToWitnessWithdrawals(IList<WithdrawalEntry>? withdrawals)
        {
            if (withdrawals == null) return null;

            var result = new List<BlockWithdrawal>(withdrawals.Count);
            foreach (var w in withdrawals)
            {
                result.Add(new BlockWithdrawal
                {
                    Index = w.Index,
                    ValidatorIndex = w.ValidatorIndex,
                    Address = w.Address,
                    AmountInGwei = (ulong)w.AmountGwei
                });
            }
            return result;
        }
    }
}
