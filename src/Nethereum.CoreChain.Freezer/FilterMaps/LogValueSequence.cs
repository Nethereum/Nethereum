using System;
using System.Collections.Generic;
using Nethereum.Freezer.FilterMaps;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    public static class LogValueSequence
    {
        public static IEnumerable<LogValueEntry> Enumerate(
            long fromBlock,
            long toBlock,
            long startLvIndex,
            long? precedingBlock,
            IChainView chain,
            FilterMapsParams p,
            bool deferValueHashing = false)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            var lvIndex = startLvIndex;

            if (precedingBlock.HasValue)
            {
                yield return LogValueEntry.Delimiter(precedingBlock.Value, lvIndex);
                lvIndex++;
            }

            for (var blockNumber = fromBlock; blockNumber <= toBlock; blockNumber++)
            {
                if (blockNumber > fromBlock)
                {
                    yield return LogValueEntry.Delimiter(blockNumber - 1, lvIndex);
                    lvIndex++;
                }

                var receipts = chain.Receipts(blockNumber);
                for (var r = 0; r < receipts.Count; r++)
                {
                    var logs = receipts[r].Logs;
                    for (var l = 0; l < logs.Count; l++)
                    {
                        var log = logs[l];
                        var groupSize = 1 + log.Topics.Count;
                        var remaining = p.ValuesPerMap - (int)(lvIndex % p.ValuesPerMap);
                        if (groupSize > remaining)
                            lvIndex += remaining;

                        var addressBytes = log.Address.HexToByteArray();
                        var addressValue = deferValueHashing ? addressBytes : LogValueHasher.AddressValue(addressBytes);
                        yield return LogValueEntry.Value(blockNumber, lvIndex, addressValue);
                        lvIndex++;

                        for (var t = 0; t < log.Topics.Count; t++)
                        {
                            var topicBytes = log.Topics[t];
                            var topicValue = deferValueHashing ? topicBytes : LogValueHasher.TopicValue(topicBytes);
                            yield return LogValueEntry.Value(blockNumber, lvIndex, topicValue);
                            lvIndex++;
                        }
                    }
                }
            }
        }
    }
}
