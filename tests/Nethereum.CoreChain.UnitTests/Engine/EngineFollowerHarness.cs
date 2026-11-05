using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Forks;
using Nethereum.Util;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.UnitTests.Import;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs.Engine;
using EngineWithdrawal = Nethereum.RPC.Eth.DTOs.Withdrawal;

namespace Nethereum.CoreChain.UnitTests.Engine
{
    internal sealed class RecordingExecutor : IBlockExecutor
    {
        private readonly IBlockExecutor _inner;

        public RecordingExecutor(IBlockExecutor inner) { _inner = inner; }

        public List<ulong> Executed { get; } = new();
        public Func<Task> BeforeProcess { get; set; }

        public int ExecutedCount { get { lock (Executed) return Executed.Count; } }

        public async Task<BlockImporterResult> ProcessBlockAsync(
            BlockHeader header, IList<ISignedTransaction> transactions, IList<BlockHeader> uncles,
            IList<Withdrawal> withdrawals, CancellationToken ct)
        {
            if (BeforeProcess != null) await BeforeProcess();
            lock (Executed) Executed.Add((ulong)header.BlockNumber);
            return await _inner.ProcessBlockAsync(header, transactions, uncles, withdrawals, ct);
        }

    }

    internal sealed class CapturingSink : IEngineForkchoiceSink
    {
        private readonly IEngineForkchoiceSink _forwardTo;

        public CapturingSink(IEngineForkchoiceSink forwardTo = null) { _forwardTo = forwardTo; }

        public List<(byte[] Head, ulong HeadNumber, byte[] Finalized)> Published { get; } = new();

        public void Publish(EngineForkchoiceHead head, EngineForkchoiceHead finalized)
        {
            Published.Add((head.Hash, (ulong)head.Header.BlockNumber, finalized?.Hash));
            _forwardTo?.Publish(head, finalized);
        }
    }

    internal sealed class EngineFollowerHarness : IDisposable
    {
        public SideChainHarness Store { get; private set; }
        public RecordingExecutor Executor { get; private set; }
        public AsyncGate Gate { get; private set; }
        public CapturingSink Sink { get; private set; }
        public EnginePayloadStash Stash { get; private set; }
        public FollowerEngineApiService Service { get; private set; }
        public byte[] GenesisHash { get; private set; }
        public Dictionary<string, BlockHeader> PeerHeaders { get; } = new();

        public static async Task<EngineFollowerHarness> CreateAsync(bool recordExecutions = true, IEngineForkchoiceSink forwardTo = null)
        {
            var harness = new EngineFollowerHarness();
            harness.Store = await SideChainHarness.CreateAsync(withSideChain: false);
            var config = harness.Store.Node.Config;
            var real = FollowerExecutorStackFactory.BuildFollowerExecutorStack(
                harness.Store.Bundle, new FixedChainActivations(HardforkName.Cancun),
                _ => config, _ => config.GetHardforkConfig(), NoRewardPolicy.Instance);
            harness.Executor = new RecordingExecutor(real);
            harness.Gate = new AsyncGate();
            harness.Gate.Publish(recordExecutions ? harness.Executor : real);
            harness.GenesisHash = await harness.Store.Bundle.Blocks.GetHashByNumberAsync(BigInteger.Zero);
            harness.Store.Bundle.Metadata.Commit(0, harness.GenesisHash);
            harness.Gate.SetHead(0, harness.GenesisHash);
            harness.Sink = new CapturingSink(forwardTo);
            harness.Stash = new EnginePayloadStash();
            var resolver = new EngineHeaderResolver(harness.Store.Bundle.Blocks, harness.Stash, (hash, ct) =>
                Task.FromResult(harness.PeerHeaders.TryGetValue(hash.ToHex(true), out var header) ? header : null));
            harness.Service = new FollowerEngineApiService(
                harness.Store.Bundle, harness.Gate, config,
                new FollowerOptions(StartBlock: 0, CheckpointEvery: 0, AnchorEvery: 0),
                harness.Stash, resolver, harness.Sink);
            return harness;
        }

        public static async Task<List<ProducedBlock>> AuthorChainAsync(string recipient, int count)
        {
            using var author = await SideChainHarness.CreateAsync(withSideChain: false);
            return await author.ProduceChainAsync(recipient, count);
        }

        public static async Task<(List<ProducedBlock> Common, ProducedBlock BranchA, ProducedBlock BranchB)> AuthorSiblingsAsync(int commonLength)
        {
            using var a = await SideChainHarness.CreateAsync(withSideChain: false);
            using var b = await SideChainHarness.CreateAsync(withSideChain: false);
            var common = await a.ProduceChainAsync(SideChainHarness.RecipientA, commonLength);
            foreach (var block in common) await b.ImportAsync(block);
            var branchA = await a.ProduceAsync(SideChainHarness.RecipientA);
            var branchB = await b.ProduceAsync(SideChainHarness.RecipientB);
            return (common, branchA, branchB);
        }

        public static string ZeroRoot => "0x" + new string('0', 64);

        public static ForkchoiceStateV1 State(byte[] head, byte[] finalized = null) => new ForkchoiceStateV1
        {
            HeadBlockHash = head.ToHex(true),
            SafeBlockHash = head.ToHex(true),
            FinalizedBlockHash = (finalized ?? new byte[32]).ToHex(true)
        };

        public static ExecutionPayloadV3 PayloadOf(ProducedBlock block)
        {
            var header = block.Header;
            var transactions = new List<string>();
            foreach (var tx in block.Transactions) transactions.Add(tx.GetRLPEncoded().ToHex(true));

            var withdrawals = new List<EngineWithdrawal>();
            if (block.Withdrawals != null)
            {
                foreach (var w in block.Withdrawals)
                {
                    withdrawals.Add(new EngineWithdrawal
                    {
                        Index = new HexBigInteger(w.Index),
                        ValidatorIndex = new HexBigInteger(w.ValidatorIndex),
                        Address = w.Address.ToHex(true),
                        Amount = new HexBigInteger(w.AmountInGwei)
                    });
                }
            }

            return new ExecutionPayloadV3
            {
                ParentHash = header.ParentHash.ToHex(true),
                FeeRecipient = header.Coinbase,
                StateRoot = header.StateRoot.ToHex(true),
                ReceiptsRoot = header.ReceiptHash.ToHex(true),
                LogsBloom = header.LogsBloom.ToHex(true),
                PrevRandao = header.MixHash.ToHex(true),
                BlockNumber = header.BlockNumber.ToHexBigInteger(),
                GasLimit = new HexBigInteger(header.GasLimit),
                GasUsed = new HexBigInteger(header.GasUsed),
                Timestamp = new HexBigInteger(header.Timestamp),
                ExtraData = (header.ExtraData ?? Array.Empty<byte>()).ToHex(true),
                BaseFeePerGas = new HexBigInteger((header.BaseFee ?? EvmUInt256.Zero).ToBigInteger()),
                BlockHash = block.Hash.ToHex(true),
                Transactions = transactions,
                Withdrawals = withdrawals,
                BlobGasUsed = new HexBigInteger(header.BlobGasUsed ?? 0),
                ExcessBlobGas = new HexBigInteger(header.ExcessBlobGas ?? 0)
            };
        }

        public ExecutionPayloadV3 Resealed(ExecutionPayloadV3 payload)
        {
            var decoded = EnginePayloadDecoder.Decode(
                payload, ZeroRoot, requestsHash: null, declaredBlockAccessListRlp: null, slotNumber: null, Store.Node.Config);
            payload.BlockHash = BlockHashCalculator.ForFork(decoded.Header, decoded.Fork).ToHex(true);
            return payload;
        }


        public Task<PayloadStatusV1> NewPayloadAsync(ProducedBlock block) =>
            Service.NewPayloadAsync(PayloadOf(block), ZeroRoot);

        public Task<ForkchoiceUpdatedResponseV1> ForkchoiceAsync(byte[] head, byte[] finalized = null) =>
            Service.ForkchoiceUpdatedAsync(State(head, finalized), attributes: null);

        public Task<BigInteger> BalanceOfAsync(string address) => Store.BalanceOfAsync(address);

        public void Dispose() => Store.Dispose();
    }
}
