using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.AppChain.Sync;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.AppChain.Sequencer
{
    public class AppChainReplicaNode : ChainNodeBase, IDisposable
    {
        private readonly IAppChain _appChain;
        private readonly ISequencerTxProxy _txProxy;
        private readonly CoordinatedSyncService? _syncService;
        private readonly AppChainReplicaConfig _config;
        private bool _disposed;

        public AppChainReplicaNode(
            IAppChain appChain,
            ISequencerTxProxy txProxy,
            AppChainReplicaConfig config,
            CoordinatedSyncService? syncService = null,
            IFilterStore? filterStore = null)
            : base(
                appChain.Blocks,
                appChain.Transactions,
                appChain.Receipts,
                appChain.Logs,
                appChain.State,
                filterStore ?? new InMemoryFilterStore(),
                new TransactionProcessor(
                    appChain.State,
                    appChain.Blocks,
                    appChain.Config,
                    new TransactionVerificationAndRecoveryImp()),
                new TransactionVerificationAndRecoveryImp(),
                new StateStoreNodeDataService(appChain.State, appChain.Blocks))
        {
            _appChain = appChain ?? throw new ArgumentNullException(nameof(appChain));
            _txProxy = txProxy ?? throw new ArgumentNullException(nameof(txProxy));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _syncService = syncService;
        }

        public IAppChain AppChain => _appChain;
        public override ChainConfig Config => _appChain.Config;
        public CoordinatedSyncService? SyncService => _syncService;

        public SyncMode SyncMode => _syncService?.Mode ?? SyncMode.Idle;
        public bool IsSyncing => SyncMode == SyncMode.BatchSync || SyncMode == SyncMode.LiveSync;

        public event EventHandler<TransactionForwardedEventArgs>? TransactionForwarded;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_syncService != null && _config.AutoStartSync)
            {
                await _syncService.StartAsync(cancellationToken);
            }
        }

        public async Task StopAsync()
        {
            if (_syncService != null)
            {
                await _syncService.StopAsync();
            }
        }

        public override async Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx)
        {
            byte[] rawTx;
            try
            {
                rawTx = tx.GetRLPEncoded();
            }
            catch (Exception ex)
            {
                return new TransactionExecutionResult
                {
                    Transaction = tx,
                    Success = false,
                    RevertReason = $"Failed to encode transaction: {ex.Message}"
                };
            }

            try
            {
                var txHash = await _txProxy.SendRawTransactionAsync(rawTx);

                OnTransactionForwarded(new TransactionForwardedEventArgs
                {
                    TransactionHash = txHash,
                    SequencerRpcUrl = _config.SequencerRpcUrl
                });

                var receiptInfo = await _txProxy.WaitForReceiptAsync(
                    txHash,
                    _config.TxConfirmationTimeoutMs,
                    _config.TxPollIntervalMs);

                return new TransactionExecutionResult
                {
                    Transaction = tx,
                    TransactionHash = txHash,
                    Success = receiptInfo?.Receipt?.HasSucceeded ?? false,
                    Receipt = receiptInfo?.Receipt,
                    ContractAddress = receiptInfo?.ContractAddress
                };
            }
            catch (Exception ex)
            {
                return new TransactionExecutionResult
                {
                    Transaction = tx,
                    TransactionHash = tx.Hash,
                    Success = false,
                    RevertReason = ex.Message
                };
            }
        }

        public override Task<List<ISignedTransaction>> GetPendingTransactionsAsync()
        {
            return Task.FromResult(new List<ISignedTransaction>());
        }

        protected override async Task<BlockContext> GetBlockContextForCallAsync()
        {
            var latestBlock = await _appChain.GetLatestBlockAsync();
            return new BlockContext
            {
                BlockNumber = latestBlock?.BlockNumber ?? 0,
                Timestamp = latestBlock?.Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Coinbase = _appChain.Config.Coinbase,
                GasLimit = _appChain.Config.BlockGasLimit,
                BaseFee = _appChain.Config.BaseFee,
                ChainId = _appChain.Config.ChainId,
                Difficulty = 0
            };
        }

        private void OnTransactionForwarded(TransactionForwardedEventArgs args)
        {
            TransactionForwarded?.Invoke(this, args);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _syncService?.Dispose();
        }
    }

    public class TransactionForwardedEventArgs : EventArgs
    {
        public byte[] TransactionHash { get; set; } = Array.Empty<byte>();
        public string SequencerRpcUrl { get; set; } = "";
    }
}
