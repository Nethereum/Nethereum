using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Util;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessageProcessor : IMessageProcessor
    {
        private readonly IMessageMerkleAccumulator _accumulator;
        private readonly IMessageResultStore? _resultStore;
        private readonly Func<MessageInfo, Task<MessageExecutionResult>>? _executor;
        private readonly ILogger<MessageProcessor>? _logger;

        public IMessageMerkleAccumulator Accumulator => _accumulator;
        public IMessageResultStore? ResultStore => _resultStore;

        public MessageProcessor(
            IMessageMerkleAccumulator accumulator,
            IMessageResultStore? resultStore = null,
            Func<MessageInfo, Task<MessageExecutionResult>>? executor = null,
            ILogger<MessageProcessor>? logger = null)
        {
            _accumulator = accumulator ?? throw new ArgumentNullException(nameof(accumulator));
            _resultStore = resultStore;
            _executor = executor;
            _logger = logger;
        }

        public async Task<MessageBatchResult> ProcessBatchAsync(IReadOnlyList<MessageInfo> messages)
        {
            var result = new MessageBatchResult();

            foreach (var message in messages)
            {
                try
                {
                    MessageExecutionResult execResult;
                    bool executorFailed = false;

                    if (_executor != null)
                    {
                        try
                        {
                            execResult = await _executor(message);
                        }
                        catch (Exception exExec)
                        {
                            _logger?.LogWarning(exExec,
                                "Executor failed for message {MessageId} from chain {SourceChainId}, recording as failed",
                                message.MessageId, message.SourceChainId);
                            executorFailed = true;
                            execResult = new MessageExecutionResult
                            {
                                TxHash = Sha3Keccack.Current.CalculateHash(
                                    System.Text.Encoding.UTF8.GetBytes(
                                        $"FAILED:{message.SourceChainId}:{message.MessageId}")),
                                Success = false,
                                GasUsed = 0
                            };
                        }
                    }
                    else
                    {
                        execResult = new MessageExecutionResult
                        {
                            TxHash = Sha3Keccack.Current.CalculateHash(
                                System.Text.Encoding.UTF8.GetBytes(
                                    $"{message.SourceChainId}:{message.MessageId}")),
                            Success = true,
                            GasUsed = 21000
                        };
                    }

                    var dataHash = message.Data.Length > 0
                        ? Sha3Keccack.Current.CalculateHash(message.Data)
                        : new byte[32];

                    var leaf = new MessageLeaf
                    {
                        SourceChainId = message.SourceChainId,
                        MessageId = message.MessageId,
                        AppChainTxHash = execResult.TxHash,
                        Success = execResult.Success,
                        DataHash = dataHash
                    };

                    int leafIndex = _accumulator.AppendLeaf(message.SourceChainId, leaf);

                    if (_resultStore != null)
                    {
                        await _resultStore.StoreAsync(new MessageResult
                        {
                            SourceChainId = message.SourceChainId,
                            MessageId = message.MessageId,
                            LeafIndex = leafIndex,
                            TxHash = execResult.TxHash,
                            Success = execResult.Success,
                            DataHash = dataHash
                        });
                    }

                    result.Results.Add(new MessageProcessingResult
                    {
                        SourceChainId = message.SourceChainId,
                        MessageId = message.MessageId,
                        Target = message.Target,
                        Success = execResult.Success,
                        AppChainTxHash = execResult.TxHash,
                        ReturnDataHash = dataHash,
                        GasUsed = execResult.GasUsed
                    });
                    result.ProcessedCount++;

                    if (!execResult.Success)
                        result.FailedCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Critical failure processing message {MessageId} from chain {SourceChainId}",
                        message.MessageId, message.SourceChainId);

                    result.Results.Add(new MessageProcessingResult
                    {
                        SourceChainId = message.SourceChainId,
                        MessageId = message.MessageId,
                        Target = message.Target,
                        Success = false
                    });
                    result.ProcessedCount++;
                    result.FailedCount++;
                }
            }

            return result;
        }
    }

    public class MessageExecutionResult
    {
        public byte[] TxHash { get; set; } = Array.Empty<byte>();
        public bool Success { get; set; }
        public long GasUsed { get; set; }
    }
}
