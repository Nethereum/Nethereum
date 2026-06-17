using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Witness;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// Canonical Ethereum block state-transition engine. Owns every step
    /// of <c>π(σ, B)</c> (Yellow Paper §11): pre-tx system calls (EIP-4788
    /// beacon root at Cancun+, EIP-2935 parent-hash history at Prague+),
    /// the DAO hard-fork drain (mainnet only, block 1,920,000), the tx
    /// execution loop, EIP-4895 withdrawals (Shanghai+), the configurable
    /// reward policy, and the post-state root compute. Returns everything
    /// the wrappers (<see cref="BlockImporter"/> for followers,
    /// <c>BlockProducer</c> for sequencers,
    /// <c>ChainNodeBase.CaptureBlockWitnessAsync</c> for read-only witness
    /// capture) need to validate, persist, or report. The engine never
    /// persists block bodies, receipts, logs, or chain-tip metadata —
    /// those are the caller's concern.
    ///
    /// <para><b>Symmetry contract.</b> The state-transition steps in this
    /// host engine MUST mirror those in
    /// <see cref="EVM.Execution.BlockExecutor"/> (the Zisk guest executor at
    /// <c>src/Nethereum.EVM.Core/Execution/BlockExecutor.cs</c>) whenever
    /// AppChain Zisk proving is in scope. Both implementations cover the
    /// same canonical state transition with different state interfaces
    /// (<see cref="IStateStore"/> here vs <c>InMemoryStateReader</c> in
    /// the guest). Steps present in both today: EIP-4788, EIP-2935, the
    /// tx loop, EIP-4895 withdrawals. Mainnet pre-Merge rewards and the
    /// DAO drain are host-only by design — AppChain does not run them and
    /// the Zisk guest does not need them. Any new state-transition step
    /// added to one MUST be added to the other.</para>
    ///
    /// <para><b>Journal lifecycle.</b> The engine does NOT touch
    /// <see cref="IHistoricalStateProvider.SetCurrentBlockNumber"/> /
    /// <see cref="IHistoricalStateProvider.ClearCurrentBlockNumberAsync"/>.
    /// Reverse-diff bracketing is a persistence concern owned by the
    /// caller wrapper (<see cref="BlockImporter"/> and <c>BlockProducer</c>
    /// arm the journal around their <see cref="ExecuteAsync"/> calls; the
    /// read-only witness-capture path does not).</para>
    /// </summary>
    public sealed class BlockExecutor
    {
        private readonly IStateStore _stateStore;
        private readonly IBlockStore _blockStore;
        private readonly IChainActivations _activations;
        private readonly Func<HardforkName, ChainConfig> _chainConfigFactory;
        private readonly Func<HardforkName, HardforkConfig> _hardforkConfigFactory;
        private readonly IIncrementalStateRootCalculator _stateRootCalculator;
        private readonly IRewardPolicy _rewardPolicy;
        private readonly ITrieNodeStore _trieNodeStore;
        private readonly ILogger<BlockExecutor>? _logger;

        public BlockExecutor(
            IStateStore stateStore,
            IBlockStore blockStore,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            IIncrementalStateRootCalculator stateRootCalculator,
            IRewardPolicy rewardPolicy,
            ITrieNodeStore trieNodeStore,
            ILogger<BlockExecutor>? logger = null)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _activations = activations ?? throw new ArgumentNullException(nameof(activations));
            _chainConfigFactory = chainConfigFactory ?? throw new ArgumentNullException(nameof(chainConfigFactory));
            _hardforkConfigFactory = hardforkConfigFactory ?? throw new ArgumentNullException(nameof(hardforkConfigFactory));
            _stateRootCalculator = stateRootCalculator ?? throw new ArgumentNullException(nameof(stateRootCalculator));
            _rewardPolicy = rewardPolicy ?? throw new ArgumentNullException(nameof(rewardPolicy));
            _trieNodeStore = trieNodeStore ?? throw new ArgumentNullException(nameof(trieNodeStore));
            _logger = logger;
        }

        /// <summary>
        /// Canonical engine entry. The caller supplies the block header,
        /// the ordered tx list (with optional cached senders), the uncle
        /// list, the withdrawal list, and per-call options. Returns
        /// <see cref="BlockExecutionResult"/> with pre/post state roots,
        /// per-tx receipts, the aggregated block bloom, witness bytes if
        /// requested, and any error / mismatch surface. Per
        /// <see cref="BlockExecutionOptions.ReadOnly"/>, all writes are
        /// absorbed by a <see cref="ReadOnlyStateStoreWrapper"/> so nothing
        /// reaches the underlying <see cref="IStateStore"/> or
        /// <see cref="ITrieNodeStore"/>.
        /// </summary>
        public async Task<BlockExecutionResult> ExecuteAsync(
            BlockHeader header,
            IReadOnlyList<TxEntry> txs,
            IList<BlockHeader>? uncles,
            IList<WithdrawalEntry>? withdrawals,
            BlockExecutionOptions options,
            CancellationToken ct = default)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (txs == null) throw new ArgumentNullException(nameof(txs));
            options ??= new BlockExecutionOptions();

            // ReadOnly mode wraps the state store so every Save / Delete /
            // Clear is absorbed in memory. The tx loop still observes its
            // own writes (overlay reads cascade through); the underlying
            // store is never mutated. We skip the post-state root compute
            // in this mode to avoid the calculator's trie-node writes via
            // _trieNodeStore — witness-capture callers do not need the
            // post-root.
            IStateStore stateForBlock = options.ReadOnly
                ? new ReadOnlyStateStoreWrapper(_stateStore)
                : _stateStore;

            ulong blockNumber = (ulong)header.BlockNumber;
            ulong timestamp = (ulong)header.Timestamp;
            var fork = _activations.ResolveAt((long)blockNumber, timestamp);

            byte[]? preStateRoot = await ResolveParentStateRootAsync(header).ConfigureAwait(false);

            var receipts = new List<TransactionExecutionResult>(txs.Count);
            var logs = new List<Log>();
            byte[]? blockBloom = null;
            BigInteger minerRewardCredited = BigInteger.Zero;
            int withdrawalsCredited = 0;
            bool stateRootMismatch = false;
            byte[]? postStateRoot = null;
            byte[]? witnessBytes = null;
            Exception? exception = null;
            string? errorMessage = null;
            WitnessRecordingStateReader? witnessRecorder = null;

            try
            {
                // Construct the witness recorder up-front so the EIP-4788 /
                // EIP-2935 system calls below can route their pre-write
                // warming reads through it. Without this, the BeaconRoots
                // and HistoryStorage contracts (their account + code +
                // pre-block slot values) never enter the witness, and the
                // Zisk guest's bytecode-replay path silently no-ops on the
                // missing contract — producing a divergent post-state.
                if (options.CaptureWitness)
                {
                    var baseReader = new StateStoreNodeDataService(stateForBlock, _blockStore);
                    witnessRecorder = new WitnessRecordingStateReader(baseReader);
                }

                // 1. Pre-tx system call (Cancun+ EIP-4788 beacon-root contract).
                // The contract at 0x000F3DF6… implements a ring buffer keyed
                // by timestamp % 8191. When called by SYSTEM_ADDRESS:
                //   storage[timestamp % 8191]        = timestamp
                //   storage[timestamp % 8191 + 8191] = parentBeaconBlockRoot
                // We apply the writes directly (matches the contract bytecode's
                // effect). When capturing a witness, the helper warm-reads the
                // contract account + code + slots through the recorder so the
                // Zisk guest can replay this block via the guest's EVM path
                // without missing the contract presence.
                var parentBeaconBlockRoot = options.ParentBeaconBlockRoot ?? header.ParentBeaconBlockRoot;
                if (fork >= HardforkName.Cancun && parentBeaconBlockRoot != null)
                {
                    await Eip4788Helpers.ApplyAsync(stateForBlock, (BigInteger)(ulong)header.Timestamp, parentBeaconBlockRoot, witnessRecorder);
                }

                // 1a. EIP-2935 (Prague+) — record the parent block hash at
                // history_contract.storage[(parent.number) % 8191] so BLOCKHASH
                // resolves from state. Same direct-write + warm-read pattern as
                // EIP-4788 above.
                if (fork >= HardforkName.Prague
                    && header.BlockNumber > 0
                    && header.ParentHash != null && header.ParentHash.Length == 32)
                {
                    var parentNumber = (BigInteger)((ulong)header.BlockNumber - 1);
                    await Eip2935Helpers.ApplyAsync(stateForBlock, parentNumber, header.ParentHash, witnessRecorder);
                }

                // 1b. DAO hard-fork forced state transition (mainnet only,
                //     exactly at block 1,920,000). Geth ref:
                //     consensus/misc/dao.go ApplyDAOHardFork. 58 child-DAO
                //     accounts plus the DAO contract itself are drained into
                //     the WithdrawDAO contract. EVM rules at this block
                //     equal Homestead — DAO is consensus-only, not an EVM
                //     change. Gated on chainId via _activations type so
                //     testnets and AppChains never run it. Excluded from
                //     Zisk zkVM builds via the csproj file-allowlist
                //     (Nethereum.EVM.Zisk.csproj only pulls in the state-
                //     root calculator files from CoreChain, not the full
                //     BlockExecutor pipeline).
                if (_activations is Nethereum.EVM.MainnetChainActivations &&
                    (long)blockNumber == Nethereum.EVM.MainnetChainActivations.DaoForkBlock)
                {
                    await ApplyDaoForkDrainAsync(stateForBlock, ct);
                }

                // 2. Transaction execution loop.
                // Pass eip158EmptyAccountPruning = (fork >= SpuriousDragon) so
                // pre-EIP-158 forks (Frontier through Tangerine Whistle) still
                // record empty contracts created by successful CREATE init code.
                var chainConfig = _chainConfigFactory(fork);
                var hardforkConfig = _hardforkConfigFactory(fork);
                var txVerifier = new Nethereum.Signer.TransactionVerificationAndRecoveryImp();
                var txProcessor = new TransactionProcessor(stateForBlock, _blockStore, chainConfig, txVerifier, hardforkConfig,
                    eip158EmptyAccountPruning: fork >= HardforkName.SpuriousDragon);

                // Initialise the state-root calculator from the parent
                // header's StateRoot so the trie lazy-loads from the trie
                // node store instead of walking every flat account on first
                // compute. No-op once already initialised. Falls through to
                // the no-prior-root path when the parent root is unavailable
                // (genesis). Skipped in ReadOnly mode since the post-state
                // compute is skipped entirely.
                if (!options.ReadOnly && preStateRoot != null && preStateRoot.Length > 0)
                {
                    await _stateRootCalculator.ComputeStateRootAsync(preStateRoot).ConfigureAwait(false);
                }

                IStateReader? stateReaderForExecution = witnessRecorder;

                BigInteger cumulativeGasUsed = 0;
                var combinedBloom = new byte[256];
                bool anyReceipt = false;

                var blockContext = BuildBlockContext(header, chainConfig);

                for (int i = 0; i < txs.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = txs[i];
                    var txResult = await txProcessor.ExecuteTransactionAsync(
                        entry.Tx,
                        blockContext,
                        i,
                        (long)cumulativeGasUsed,
                        entry.CachedSender,
                        stateReaderForExecution,
                        traceEnabled: options.TraceTxIndex == i).ConfigureAwait(false);

                    cumulativeGasUsed = txResult.CumulativeGasUsed;
                    receipts.Add(txResult);
                    if (txResult.Receipt != null)
                    {
                        anyReceipt = true;
                        CombineBloom(combinedBloom, txResult.Receipt.Bloom);
                    }
                    if (txResult.Logs != null && txResult.Logs.Count > 0)
                    {
                        logs.AddRange(txResult.Logs);
                    }
                }

                if (anyReceipt) blockBloom = combinedBloom;

                // 2a. EIP-7685 end-of-block system calls (Prague+).
                // Two predeploys are invoked by SYSTEM_ADDRESS with empty
                // calldata and a fixed 30M-gas budget. Each predeploy mutates
                // its own ring-buffer storage to dequeue serviced requests
                // and returns the serialised request list as call output.
                // Per EIP-7685 a failure of any system call is a consensus
                // error — the block is invalid. The return data is
                // discarded for now; once the requests_hash header field is
                // validated end-to-end (task #259 follow-up) it will be
                // concatenated and hashed per EIP-7685.
                if (fork >= HardforkName.Prague)
                {
                    await txProcessor.ExecuteSystemCallAsync(
                        Eip7685Constants.WithdrawalRequestPredeployAddress,
                        blockContext,
                        witnessRecorder).ConfigureAwait(false);
                    await txProcessor.ExecuteSystemCallAsync(
                        Eip7685Constants.ConsolidationRequestPredeployAddress,
                        blockContext,
                        witnessRecorder).ConfigureAwait(false);
                }

                // 3. Withdrawals (Shanghai+). Each withdrawal credits its
                // target address with amount (in gwei) * 1e9 wei.
                if (withdrawals != null)
                {
                    foreach (var w in withdrawals)
                    {
                        await EthereumProofOfWorkRewardPolicy.CreditAsync(
                            stateForBlock, w.Address, w.AmountGwei * 1_000_000_000, ct).ConfigureAwait(false);
                        withdrawalsCredited++;
                    }
                }

                // 4. Reward policy. Pre-Merge mainnet credits miner + uncle
                // inclusion + uncle rewards via EthereumProofOfWorkRewardPolicy;
                // every other chain uses NoRewardPolicy (post-Merge mainnet,
                // AppChain sequencer, DevChain).
                minerRewardCredited = await _rewardPolicy
                    .ApplyAsync(header, uncles ?? new List<BlockHeader>(), stateForBlock, fork, ct)
                    .ConfigureAwait(false);

                // 5. Recompute state root after rewards & withdrawals. Skipped
                // in ReadOnly mode — the calculator writes trie nodes via
                // ITrieNodeStore.Put on this path which we MUST suppress when
                // ReadOnly is set.
                if (!options.ReadOnly)
                {
                    postStateRoot = await _stateRootCalculator.ComputeStateRootAsync().ConfigureAwait(false);
                    stateRootMismatch = header.StateRoot != null
                        && postStateRoot != null
                        && !ByteUtil.AreEqual(postStateRoot, header.StateRoot);
                }

                // 6. Serialise the witness if requested.
                if (options.CaptureWitness && witnessRecorder != null)
                {
                    var hardforkName = fork;
                    var witnessTxs = new List<BlockWitnessTransaction>(txs.Count);
                    foreach (var entry in txs)
                    {
                        witnessTxs.Add(new BlockWitnessTransaction
                        {
                            From = entry.CachedSender ?? "",
                            RlpEncoded = entry.Tx.GetRLPEncoded()
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
                        Features = new BlockFeatureConfig
                        {
                            Fork = hardforkName,
                            MaxBlobsPerBlock = hardforkName >= HardforkName.Prague ? 9 : 6
                        },
                        Transactions = witnessTxs,
                        Accounts = witnessRecorder.GetWitnessAccounts()
                    };

                    witnessBytes = BinaryBlockWitness.Serialize(witnessData);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                exception = ex;
                errorMessage = ex.Message;
                _logger?.LogError(ex, "Block execution failed at block {BlockNumber}", header.BlockNumber);
            }

            return new BlockExecutionResult
            {
                Fork = fork,
                PreStateRoot = preStateRoot,
                PostStateRoot = postStateRoot,
                Receipts = receipts,
                Logs = logs,
                BlockBloom = blockBloom,
                WitnessBytes = witnessBytes,
                MinerRewardCredited = minerRewardCredited,
                WithdrawalsCredited = withdrawalsCredited,
                StateRootMismatch = stateRootMismatch,
                Exception = exception,
                ErrorMessage = errorMessage
            };
        }

        private async Task<byte[]?> ResolveParentStateRootAsync(BlockHeader header)
        {
            if (header == null || header.ParentHash == null || header.ParentHash.Length == 0)
                return null;
            var parent = await _blockStore.GetByHashAsync(header.ParentHash).ConfigureAwait(false);
            return parent?.StateRoot;
        }

        private static BlockContext BuildBlockContext(BlockHeader header, ChainConfig chainConfig)
        {
            // Strict coinbase: no chainConfig.Coinbase fallback. A malformed
            // block coinbase is a hard error — sequencer bug or wire
            // corruption, not something we silently paper over.
            // EIP-4399 (Paris): DIFFICULTY (0x44) is renamed PREVRANDAO and
            // pushes the parent block's RANDAO mix (= header.MixHash) instead
            // of the PoW difficulty. Pre-Paris the field still carries
            // header.Difficulty. The EVM opcode handler reads
            // programContext.Difficulty unconditionally, so the fork-aware
            // selection happens here at block-context construction time.
            var isPostMerge = Nethereum.EVM.HardforkNames.Parse(chainConfig.Hardfork) >= Nethereum.EVM.HardforkName.Paris;
            BigInteger evmDifficulty = isPostMerge && header.MixHash != null && header.MixHash.Length > 0
                ? new BigInteger(header.MixHash, isUnsigned: true, isBigEndian: true)
                : header.Difficulty;
            return new BlockContext
            {
                BlockNumber = header.BlockNumber,
                Timestamp = (long)header.Timestamp,
                Coinbase = header.Coinbase,
                GasLimit = header.GasLimit,
                BaseFee = header.BaseFee ?? chainConfig.BaseFee,
                Difficulty = evmDifficulty,
                PrevRandao = header.MixHash,
                ChainId = chainConfig.ChainId,
                ExcessBlobGas = header.ExcessBlobGas ?? 0
            };
        }

        private static void CombineBloom(byte[] target, byte[]? source)
        {
            if (source == null || source.Length != 256) return;
            for (int i = 0; i < 256; i++) target[i] |= source[i];
        }

        /// <summary>
        /// Apply the DAO hard-fork forced state transition at mainnet block
        /// 1,920,000. Moves the balance of each of the 58 child-DAO accounts
        /// + the DAO contract itself into the WithdrawDAO contract. Mirrors
        /// geth <c>consensus/misc/dao.go ApplyDAOHardFork</c>:
        /// <code>
        /// for _, addr := range params.DAODrainList() {
        ///     state.AddBalance(WithdrawContract, state.GetBalance(addr))
        ///     state.SetBalance(addr, common.Big0)
        /// }
        /// </code>
        /// No EVM rule change — this runs at Homestead EVM rules. The
        /// withdrawal contract did not exist before block 1,920,000 — geth's
        /// <c>state.AddBalance</c> creates an empty account on first credit.
        /// </summary>
        private static async Task ApplyDaoForkDrainAsync(IStateStore stateStore, CancellationToken ct)
        {
            BigInteger drained = BigInteger.Zero;
            foreach (var addr in Forks.DaoForkConstants.DrainList)
            {
                var acc = await stateStore.GetAccountAsync(addr);
                if (acc == null) continue;
                var bal = new BigInteger(acc.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true);
                if (bal.IsZero)
                {
                    // Geth still calls SetBalance(addr, 0) here — but since
                    // our balance is already zero AND we're not mutating any
                    // other field, the trie node for this address doesn't
                    // change. Safe to skip.
                    continue;
                }
                drained += bal;
                acc.Balance = EvmUInt256.Zero;
                await stateStore.SaveAccountAsync(addr, acc);
            }

            if (drained.IsZero) return;
            // Credit the withdraw contract. CreditAsync handles the
            // "account doesn't yet exist" case (creates an empty account
            // with the credit as balance), matching geth's AddBalance.
            await EthereumProofOfWorkRewardPolicy.CreditAsync(
                stateStore, Forks.DaoForkConstants.WithdrawContractAddress, drained, ct);
        }
    }

    /// <summary>
    /// One withdrawal entry consumed by <see cref="BlockExecutor.ExecuteAsync"/>.
    /// Top-level type so callers wiring the engine directly do not need to
    /// reach into wrapper-internal nested types. Distinct from
    /// the wire-shaped <see cref="Nethereum.Model.Withdrawal"/> (raw 20-byte
    /// address + ulong gwei) — this is the engine-internal shape used by
    /// the credit step (0x-prefixed hex address + BigInteger gwei).
    /// </summary>
    public readonly struct WithdrawalEntry
    {
        public WithdrawalEntry(string address, BigInteger amountGwei)
        {
            Address = address;
            AmountGwei = amountGwei;
        }

        public string Address { get; }
        public BigInteger AmountGwei { get; }
    }
}
