using Nethereum.CoreChain.IntegrationTests.BlockchainTests;
using Nethereum.Node.HarnessServer;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class EestBlockchainTestsRlpDriver : IConformanceDriver
    {
        public static readonly EestBlockchainTestsRlpDriver Instance = new();

        public string SuiteId => "eest-blockchain-tests-rlp";
        public string SuiteName => "Nethereum EEST blockchain_tests - consume-rlp (full node, RLP import)";

        public static readonly HardforkRegistry FixtureRegistry = Nethereum.Node.HarnessServer.HarnessNodeHost.Registry;

        private static readonly Sha3Keccack Keccak = new();

        public async Task<ConformanceCaseResult> RunAsync(BlockchainTestLoader.BlockchainTest test)
        {
            ChainForkSchedule schedule;
            try
            {
                schedule = ForkScheduleResolver.Resolve(test.Network, test.BlobSchedule);
            }
            catch (System.Exception ex)
            {
                return ConformanceCaseResult.Fail("malformedNetwork",
                    $"Test '{test.Name}' has an unrecognised Network value '{test.Network}': {ex.Message}");
            }

            var genesisFork = schedule.ResolveActivations().ResolveAt(0, 0);

            var document = EestGenesisBuilder.BuildGenesisDocument(test);

            await using var harness = await HarnessNodeHost.ComposeInProcessAsync(document, schedule).ConfigureAwait(false);
            var node = harness.Node;
            var importer = harness.Importer;

            if (test.GenesisBlockHeader.StateRoot is { Length: > 0 })
            {
                var genesisBlock = await node.Blocks.GetByNumberAsync(0).ConfigureAwait(false);
                var genesisRoot = genesisBlock?.StateRoot;
                if (genesisRoot == null || !genesisRoot.AreTheSame(test.GenesisBlockHeader.StateRoot))
                {
                    return ConformanceCaseResult.Fail("genesisStateRoot",
                        $"expected=0x{test.GenesisBlockHeader.StateRoot.ToHex()} actual=0x{genesisRoot?.ToHex()} " +
                        "(built by the real node's BlockManager.CreateGenesisBlockAsync, not read verbatim from the fixture)");
                }
            }

            if (test.GenesisBlockHeader.Hash is { Length: 32 })
            {
                var genesisBlock = await node.Blocks.GetByNumberAsync(0).ConfigureAwait(false);
                var genesisHash = genesisBlock != null ? BlockHashCalculator.ForFork(genesisBlock, genesisFork) : null;
                if (genesisHash == null || !genesisHash.AreTheSame(test.GenesisBlockHeader.Hash))
                {
                    return ConformanceCaseResult.Fail("genesisHash",
                        $"expected=0x{test.GenesisBlockHeader.Hash.ToHex()} actual=0x{genesisHash?.ToHex()} " +
                        "(the real node computed a different genesis hash than the fixture declares)");
                }
            }

            for (int blockIdx = 0; blockIdx < test.Blocks.Count; blockIdx++)
            {
                var blockData = test.Blocks[blockIdx];
                bool expectingRejection = !string.IsNullOrEmpty(blockData.ExpectException);

                if (expectingRejection && ExpectedRejectionMatcher.SkippedByDecision(blockData.ExpectException!, out var skipReason))
                    return ConformanceCaseResult.Skipped($"Block {blockIdx}: {skipReason}");

                BlockHeader header;
                IList<ISignedTransaction> transactions;
                IList<BlockHeader> uncles;
                IList<Withdrawal>? withdrawals;

                if (expectingRejection)
                {
                    try
                    {
                        header = DecodeHeaderFromBlockRlp(blockData.Rlp);
                        transactions = DecodeTransactionsFromBlockRlp(blockData.Rlp);
                        uncles = DecodeUnclesFromBlockRlp(blockData.Rlp);
                        withdrawals = DecodeWithdrawalsFromBlockRlp(blockData.Rlp, header);
                    }
                    catch (Exception ex)
                    {
                        if (ExpectedRejectionMatcher.SatisfiedByDecodeFailure(blockData.ExpectException!))
                            continue;

                        if (!ExpectedRejectionMatcher.Satisfies(blockData.ExpectException!, ex, out var decodeDetail))
                        {
                            return ConformanceCaseResult.Fail("rejectionReasonMismatch",
                                $"Block {blockIdx}: expected exception=\"{blockData.ExpectException}\" but the block RLP did not decode - {decodeDetail}");
                        }

                        continue;
                    }
                }
                else
                {
                    header = ToModelHeader(blockData.BlockHeader);
                    transactions = DecodeTransactionsFromBlockRlp(blockData.Rlp);
                    uncles = DecodeUnclesFromBlockRlp(blockData.Rlp);
                    withdrawals = ConvertWithdrawals(blockData.Withdrawals);
                }

                BlockImporterResult? result = null;
                Exception? thrown = null;
                try
                {
                    result = await importer.ImportAsync(header, transactions, uncles, withdrawals,
                            blockData.BlockAccessList, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }

                if (expectingRejection)
                {
                    if (thrown == null && result is { BlockHash: not null } && Accepted(result))
                    {
                        return ConformanceCaseResult.Fail("rejectionNotEnforced",
                            $"Block {blockIdx} ACCEPTED a block that must be rejected (expected exception=\"{blockData.ExpectException}\")");
                    }

                    var observed = ObservedRejection.FromImport(
                        result?.FailedValidityChecks,
                        result?.InvalidTransactionReason ?? TransactionError.None,
                        thrown ?? result?.Exception);

                    if (!ExpectedRejectionMatcher.Satisfies(blockData.ExpectException!, observed, out var rejectionDetail))
                    {
                        return ConformanceCaseResult.Fail("rejectionReasonMismatch",
                            $"Block {blockIdx}: expected exception=\"{blockData.ExpectException}\" but {rejectionDetail}");
                    }

                    continue;
                }

                if (thrown != null)
                    return ConformanceCaseResult.Fail("exception", $"Block {blockIdx}: {thrown.GetType().Name}: {thrown.Message}");

                if (result == null || !Accepted(result))
                {
                    var kind = result?.FailedChecks.Count > 0 ? string.Join("+", result.FailedChecks) : "unknown";
                    var detail = $"Block {blockIdx}: {string.Join(",", result?.FailedChecks ?? new List<string>())} " +
                                 $"- expectedStateRoot=0x{result?.ExpectedStateRoot?.ToHex()} actualStateRoot=0x{result?.ComputedStateRoot?.ToHex()}";

                    if (result?.BlockAccessListHashMismatch == true)
                        detail += $" - BAL {BlockAccessListDiff.Summary(result.BlockAccessList, blockData.BlockAccessList)}";

                    if (result?.Exception != null)
                        detail += $" - engine exception: {result.Exception.GetType().Name}: {result.Exception.Message}";

                    return ConformanceCaseResult.Fail(kind, detail);
                }

                if (blockData.BlockHeader.Hash is { Length: 32 })
                {
                    var recomputedHash = Keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                    if (!recomputedHash.AreTheSame(blockData.BlockHeader.Hash))
                    {
                        return ConformanceCaseResult.Fail("blockHash",
                            $"Block {blockIdx}: header codec hash mismatch expected=0x{blockData.BlockHeader.Hash.ToHex()} actual=0x{recomputedHash.ToHex()}");
                    }
                }
            }

            var headVerdict = await VerifyHeadAndPostStateAsync(test, node).ConfigureAwait(false);
            return headVerdict ?? ConformanceCaseResult.Ok();
        }

        private static bool Accepted(BlockImporterResult result) =>
            result.RootMatches
            || result.Disposition == BlockImportDisposition.StoredAsSide
            || result.Disposition == BlockImportDisposition.AlreadySide;

        private static async Task<ConformanceCaseResult?> VerifyHeadAndPostStateAsync(
            BlockchainTestLoader.BlockchainTest test, Nethereum.DevChain.DevChainNode node)
        {
            if (test.LastBlockHash is { Length: 32 })
            {
                var head = await node.Blocks.GetLatestAsync().ConfigureAwait(false);
                var headHash = head == null
                    ? null
                    : await node.Blocks.GetHashByNumberAsync(head.BlockNumber.ToBigInteger()).ConfigureAwait(false);
                if (headHash == null || !headHash.AreTheSame(test.LastBlockHash))
                {
                    return ConformanceCaseResult.Fail("lastBlockHash",
                        $"expected=0x{test.LastBlockHash.ToHex()} actual=0x{headHash?.ToHex()} " +
                        $"(head number {head?.BlockNumber})");
                }
            }

            foreach (var expected in test.PostState)
            {
                var address = expected.Key;
                var expectedAccount = expected.Value;
                var account = await node.State.GetAccountAsync(address).ConfigureAwait(false);
                if (account == null)
                {
                    if (expectedAccount.Balance.IsZero && expectedAccount.Nonce.IsZero
                        && expectedAccount.Code.Length == 0 && expectedAccount.Storage.Count == 0)
                        continue;
                    return ConformanceCaseResult.Fail("postState", $"account {address} is missing from the live state");
                }

                if (account.Balance.ToBigInteger() != expectedAccount.Balance)
                    return ConformanceCaseResult.Fail("postState",
                        $"account {address} balance expected={expectedAccount.Balance} actual={account.Balance.ToBigInteger()}");
                if (account.Nonce.ToBigInteger() != expectedAccount.Nonce)
                    return ConformanceCaseResult.Fail("postState",
                        $"account {address} nonce expected={expectedAccount.Nonce} actual={account.Nonce.ToBigInteger()}");

                var code = await node.State.GetCodeAsync(account.CodeHash).ConfigureAwait(false) ?? Array.Empty<byte>();
                if (!code.AreTheSame(expectedAccount.Code))
                    return ConformanceCaseResult.Fail("postState", $"account {address} code differs");

                foreach (var slot in expectedAccount.Storage)
                {
                    var stored = await node.State.GetStorageAsync(address, slot.Key).ConfigureAwait(false);
                    var actual = stored == null || stored.Length == 0
                        ? System.Numerics.BigInteger.Zero
                        : new System.Numerics.BigInteger(stored, isUnsigned: true, isBigEndian: true);
                    if (actual != slot.Value)
                        return ConformanceCaseResult.Fail("postState",
                            $"account {address} slot {slot.Key} expected={slot.Value} actual={actual}");
                }
            }

            return null;
        }

        private static BlockHeader ToModelHeader(BlockchainTestLoader.BlockHeader h) => new()
        {
            ParentHash = h.ParentHash,
            UnclesHash = h.UncleHash,
            Coinbase = "0x" + h.Coinbase.ToHex(),
            StateRoot = h.StateRoot,
            TransactionsHash = h.TransactionsRoot,
            ReceiptHash = h.ReceiptsRoot,
            LogsBloom = h.LogsBloom,
            Difficulty = new EvmUInt256(h.Difficulty),
            BlockNumber = BlockchainTestLoader.ToInt64Wrapping(h.Number),
            GasLimit = BlockchainTestLoader.ToInt64Wrapping(h.GasLimit),
            GasUsed = BlockchainTestLoader.ToInt64Wrapping(h.GasUsed),
            Timestamp = BlockchainTestLoader.ToInt64Wrapping(h.Timestamp),
            ExtraData = h.ExtraData,
            MixHash = h.MixHash,
            Nonce = h.Nonce,
            BaseFee = h.BaseFee.HasValue ? BlockchainTestLoader.ToInt64Wrapping(h.BaseFee.Value) : (long?)null,
            WithdrawalsRoot = h.WithdrawalsRoot,
            BlobGasUsed = h.BlobGasUsed,
            ExcessBlobGas = h.ExcessBlobGas,
            ParentBeaconBlockRoot = h.ParentBeaconBlockRoot,
            RequestsHash = h.RequestsHash,
            BlockAccessListHash = h.BlockAccessListHash,
            SlotNumber = h.SlotNumber.HasValue ? (ulong?)(ulong)h.SlotNumber.Value : null
        };

        private static IList<Withdrawal>? ConvertWithdrawals(List<BlockchainTestLoader.WithdrawalData> withdrawals)
        {
            if (withdrawals == null || withdrawals.Count == 0) return null;
            var result = new List<Withdrawal>(withdrawals.Count);
            foreach (var w in withdrawals)
                result.Add(new Withdrawal
                {
                    Index = w.Index,
                    ValidatorIndex = w.ValidatorIndex,
                    Address = w.Address.HexToByteArray(),
                    AmountInGwei = w.Amount
                });
            return result;
        }

        private static BlockHeader DecodeHeaderFromBlockRlp(byte[] blockRlp)
        {
            var decoded = RLP.RLP.Decode(blockRlp);
            if (decoded is not RLPCollection blockList || blockList.Count < 1)
                throw new InvalidOperationException("Block RLP does not contain a header element.");

            var headerRlp = blockList[0].RLPData;
            return BlockHeaderEncoder.Current.Decode(headerRlp);
        }

        private static IList<BlockHeader> DecodeUnclesFromBlockRlp(byte[] blockRlp)
        {
            var result = new List<BlockHeader>();
            if (blockRlp == null || blockRlp.Length == 0) return result;

            var decoded = RLP.RLP.Decode(blockRlp);
            if (decoded is not RLPCollection blockList || blockList.Count < 3) return result;

            if (blockList[2] is not RLPCollection uncleList) return result;

            foreach (var uncleItem in uncleList)
                result.Add(BlockHeaderEncoder.Current.Decode(uncleItem.RLPData));

            return result;
        }

        private static IList<Withdrawal>? DecodeWithdrawalsFromBlockRlp(byte[] blockRlp, BlockHeader header)
        {
            var decoded = RLP.RLP.Decode(blockRlp);
            if (decoded is not RLPCollection blockList || blockList.Count < 4)
            {
                if (header.WithdrawalsRoot != null)
                    throw new InvalidOperationException("The block header declares a withdrawalsRoot but the block carries no withdrawals list.");
                return null;
            }

            var withdrawalList = (RLPCollection)blockList[3];
            var result = new List<Withdrawal>();
            foreach (var wRlp in withdrawalList)
                result.Add(WithdrawalEncoder.Current.Decode(wRlp.RLPData));

            return result;
        }

        private static IList<ISignedTransaction> DecodeTransactionsFromBlockRlp(byte[] blockRlp)
        {
            var result = new List<ISignedTransaction>();
            if (blockRlp == null || blockRlp.Length == 0) return result;

            var decoded = RLP.RLP.Decode(blockRlp);
            if (decoded is not RLPCollection blockList || blockList.Count < 2) return result;

            if (blockList[1] is not RLPCollection txList) return result;

            foreach (var txItem in txList)
            {
                byte[] txBytes = txItem.RLPData;
                if (txBytes == null || txBytes.Length == 0)
                {
                    if (txItem is RLPCollection txFields)
                    {
                        var fieldBytes = new byte[txFields.Count][];
                        for (int j = 0; j < txFields.Count; j++)
                            fieldBytes[j] = RLP.RLP.EncodeElement(txFields[j].RLPData);
                        txBytes = RLP.RLP.EncodeList(fieldBytes);
                    }
                }

                if (txBytes != null && txBytes.Length > 0)
                    result.Add(TransactionFactory.CreateTransaction(txBytes, allowBlobNetworkWrapper: false));
            }

            return result;
        }
    }
}
