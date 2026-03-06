using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Nethereum.AppChain.Anchoring.Messaging.Metrics
{
    public class MessagingMetrics : IDisposable
    {
        private readonly string _chainId;
        private readonly string _name;
        private readonly Meter _meter;
        private readonly Meter _detailedMeter;
        private readonly Counter<long> _messagesReceived;
        private readonly Counter<long> _messagingErrors;
        private readonly Histogram<double> _pollDuration;

        private long _messagesReceivedCount;
        private long _lastProcessedMessageId;
        private long _pendingMessages;

        public long MessagesReceivedCount => Interlocked.Read(ref _messagesReceivedCount);
        public long LastProcessedMessageId => Interlocked.Read(ref _lastProcessedMessageId);
        public long PendingMessages => Interlocked.Read(ref _pendingMessages);

        public MessagingMetrics(string chainId, string name = "Nethereum", IMeterFactory? meterFactory = null)
        {
            _chainId = chainId;
            _name = name;
            _meter = meterFactory?.Create($"{name}.Messaging") ?? new Meter($"{name}.Messaging");
            _detailedMeter = meterFactory?.Create($"{name}.Messaging.Detailed") ?? new Meter($"{name}.Messaging.Detailed");

            _messagesReceived = _meter.CreateCounter<long>(
                "messaging.messages_received",
                unit: "{message}",
                description: "Total messages received from source chains");

            _messagingErrors = _meter.CreateCounter<long>(
                "messaging.errors",
                unit: "{error}",
                description: "Total messaging errors");

            _pollDuration = _detailedMeter.CreateHistogram<double>(
                "messaging.poll.duration",
                unit: "s",
                description: "Time to poll messages from all sources"
#if NET9_0_OR_GREATER
                , advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
                }
#endif
                );

            _meter.CreateObservableGauge("messaging.last_processed_message",
                () => new Measurement<long>(Interlocked.Read(ref _lastProcessedMessageId),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{message}", description: "Last processed message ID");

            _meter.CreateObservableGauge("messaging.pending_messages",
                () => new Measurement<long>(Interlocked.Read(ref _pendingMessages),
                    new KeyValuePair<string, object?>("chain_id", _chainId),
                    new KeyValuePair<string, object?>("chain_name", _name)),
                unit: "{message}", description: "Number of pending messages");
        }

        public void RecordMessagesReceived(int count, ulong lastMessageId)
        {
            var tags = new TagList { { "chain_id", _chainId }, { "chain_name", _name } };
            _messagesReceived.Add(count, tags);
            Interlocked.Add(ref _messagesReceivedCount, count);
            Interlocked.Exchange(ref _lastProcessedMessageId, (long)lastMessageId);
        }

        public void RecordPollDuration(double durationSeconds)
        {
            _pollDuration.Record(durationSeconds, new TagList { { "chain_id", _chainId }, { "chain_name", _name } });
        }

        public void RecordError(string reason)
        {
            _messagingErrors.Add(1, new TagList { { "chain_id", _chainId }, { "chain_name", _name }, { "reason", reason } });
        }

        public void UpdatePendingMessages(long count)
        {
            Interlocked.Exchange(ref _pendingMessages, count);
        }

        public void Dispose()
        {
            _meter.Dispose();
            _detailedMeter.Dispose();
        }
    }
}
