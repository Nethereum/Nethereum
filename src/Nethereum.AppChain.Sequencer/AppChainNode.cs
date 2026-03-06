using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.AppChain.Sequencer
{
    public class AppChainNode : ChainNodeBase
    {
        private readonly IAppChain _appChain;
        private readonly ISequencer? _sequencer;
        private readonly AppChainConfig _config;

        /// <summary>
        /// Creates an AppChainNode. For follower/read-only mode, sequencer can be null.
        /// </summary>
        public AppChainNode(IAppChain appChain, ISequencer? sequencer = null, IFilterStore? filterStore = null)
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
            _sequencer = sequencer;
            _config = appChain.Config;
        }

        public IAppChain AppChain => _appChain;
        public ISequencer? Sequencer => _sequencer;

        /// <summary>
        /// Returns true if this node can accept transactions (has a sequencer).
        /// </summary>
        public bool CanAcceptTransactions => _sequencer != null;

        public override ChainConfig Config => _config;

        public override async Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx)
        {
            if (_sequencer == null)
            {
                return new TransactionExecutionResult
                {
                    Transaction = tx,
                    Success = false,
                    RevertReason = "This node is in read-only mode (follower). Send transactions to the sequencer."
                };
            }

            var txHash = await _sequencer.SubmitTransactionAsync(tx);

            return new TransactionExecutionResult
            {
                Transaction = tx,
                TransactionHash = txHash,
                Success = true
            };
        }

        public override async Task<List<ISignedTransaction>> GetPendingTransactionsAsync()
        {
            if (_sequencer == null)
            {
                return new List<ISignedTransaction>();
            }
            var pending = await _sequencer.TxPool.GetPendingAsync(_sequencer.Config.MaxTransactionsPerBlock);
            return new List<ISignedTransaction>(pending);
        }

        protected override async Task<BlockContext> GetBlockContextForCallAsync()
        {
            var latestBlock = await _appChain.GetLatestBlockAsync();
            return new BlockContext
            {
                BlockNumber = latestBlock?.BlockNumber ?? 0,
                Timestamp = latestBlock?.Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Coinbase = _config.Coinbase,
                GasLimit = _config.BlockGasLimit,
                BaseFee = _config.BaseFee,
                ChainId = _config.ChainId,
                Difficulty = 0
            };
        }

        public async Task<byte[]> ProduceBlockAsync()
        {
            if (_sequencer == null)
            {
                throw new InvalidOperationException("Cannot produce blocks in follower mode");
            }
            return await _sequencer.ProduceBlockAsync();
        }
    }
}
