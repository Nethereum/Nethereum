using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;
using Nethereum.BlockchainProcessing;
using Nethereum.BlockchainProcessing.Metrics;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Contracts;
using Nethereum.Web3;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#endif

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class HubMessageLogProcessor
    {
        private readonly IMessageIndexStore _store;
        private readonly ulong _sourceChainId;
        private readonly ulong _targetChainId;
        private readonly IBlockValidator _blockValidator;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
        private readonly ILogger _logger;
#endif

        public string RpcUrl { get; set; } = "";
        public string HubContractAddress { get; set; } = "";
        public BigInteger StartAtBlockNumberIfNotProcessed { get; set; } = 0;
        public int NumberOfBlocksPerRequest { get; set; } = 1000;
        public int RetryWeight { get; set; } = 50;
        public uint MinimumBlockConfirmations { get; set; } = 12;
        public int ReorgBuffer { get; set; } = 0;
        public ILogProcessingObserver Observer { get; set; }

        public HubMessageLogProcessor(
            IMessageIndexStore store,
            ulong sourceChainId,
            ulong targetChainId,
            IBlockValidator blockValidator = null
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
            , ILogger logger = null
#endif
            )
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _sourceChainId = sourceChainId;
            _targetChainId = targetChainId;
            _blockValidator = blockValidator;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
            _logger = logger;
#endif
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var web3 = new Web3.Web3(RpcUrl);
            var progressRepository = _store.GetBlockProgressRepository(_sourceChainId);

            var processor = web3.Processing.Logs.CreateProcessorForContract<MessageSentEventDTO>(
                HubContractAddress,
                action: async (eventLog) => await ProcessEventAsync(eventLog),
                minimumBlockConfirmations: MinimumBlockConfirmations,
                criteria: (eventLog) => eventLog.Event.TargetChainId == _targetChainId,
                blockProgressRepository: ReorgBuffer > 0
                    ? new ReorgBufferedBlockProgressRepository(progressRepository, ReorgBuffer)
                    : progressRepository
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
                , log: _logger
#endif
                );

            processor.Observer = Observer;

            await processor.ExecuteAsync(
                cancellationToken: cancellationToken,
                startAtBlockNumberIfNotProcessed: StartAtBlockNumberIfNotProcessed);
        }

        private async Task ProcessEventAsync(EventLog<MessageSentEventDTO> eventLog)
        {
            var evt = eventLog.Event;

#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
            _logger?.LogDebug("Indexed MessageSent: source={SourceChainId} msgId={MessageId} target={TargetChainId}",
                evt.SourceChainId, evt.MessageId, evt.TargetChainId);
#endif

            await _store.StoreAsync(new MessageInfo
            {
                MessageId = evt.MessageId,
                SourceChainId = evt.SourceChainId,
                TargetChainId = evt.TargetChainId,
                Sender = evt.Sender ?? "",
                Target = evt.Target ?? "",
                Data = evt.Data ?? Array.Empty<byte>(),
                Timestamp = eventLog.Log.BlockNumber != null ? (long)eventLog.Log.BlockNumber.Value : 0
            });
        }
    }
}
