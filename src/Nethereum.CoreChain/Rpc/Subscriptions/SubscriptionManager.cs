using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Models;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Subscriptions
{
    public class SubscriptionManager
    {
        private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();
        private long _idCounter;

        public string Subscribe(string connectionId, SubscriptionType type, LogFilter logFilter = null)
        {
            var subId = GenerateSubscriptionId();
            var sub = new Subscription
            {
                Id = subId,
                ConnectionId = connectionId,
                Type = type,
                LogFilter = logFilter
            };
            _subscriptions[subId] = sub;
            return subId;
        }

        public bool Unsubscribe(string subscriptionId, string connectionId)
        {
            if (_subscriptions.TryGetValue(subscriptionId, out var sub) && sub.ConnectionId == connectionId)
            {
                return _subscriptions.TryRemove(subscriptionId, out _);
            }
            return false;
        }

        public void RemoveAllForConnection(string connectionId)
        {
            var toRemove = _subscriptions.Where(kvp => kvp.Value.ConnectionId == connectionId).Select(kvp => kvp.Key).ToList();
            foreach (var key in toRemove)
            {
                _subscriptions.TryRemove(key, out _);
            }
        }

        public IReadOnlyList<SubscriptionNotification> OnNewBlock(BlockHeader header, byte[] blockHash, List<FilteredLog> logs)
        {
            var notifications = new List<SubscriptionNotification>();

            foreach (var sub in _subscriptions.Values)
            {
                if (sub.Type == SubscriptionType.NewHeads)
                {
                    var headerDto = header.ToBlockWithTransactionHashes(blockHash);
                    notifications.Add(new SubscriptionNotification
                    {
                        SubscriptionId = sub.Id,
                        ConnectionId = sub.ConnectionId,
                        Payload = headerDto
                    });
                }
                else if (sub.Type == SubscriptionType.Logs && logs != null)
                {
                    foreach (var log in logs)
                    {
                        if (MatchesFilter(log, sub.LogFilter))
                        {
                            var logDto = ToFilterLog(log);
                            notifications.Add(new SubscriptionNotification
                            {
                                SubscriptionId = sub.Id,
                                ConnectionId = sub.ConnectionId,
                                Payload = logDto
                            });
                        }
                    }
                }
            }

            return notifications;
        }

        public int ActiveCount => _subscriptions.Count;

        private static bool MatchesFilter(FilteredLog log, LogFilter filter)
        {
            if (filter == null) return true;
            return filter.MatchesAddress(log.Address) && filter.MatchesTopics(log.Topics);
        }

        private static FilterLog ToFilterLog(FilteredLog log)
        {
            return new FilterLog
            {
                Address = log.Address,
                Topics = log.Topics?.Select(t => (object)t.ToHex(true)).ToArray() ?? Array.Empty<object>(),
                Data = log.Data?.ToHex(true) ?? "0x",
                BlockNumber = new HexBigInteger(log.BlockNumber),
                TransactionHash = log.TransactionHash?.ToHex(true),
                TransactionIndex = new HexBigInteger(log.TransactionIndex),
                BlockHash = log.BlockHash?.ToHex(true),
                LogIndex = new HexBigInteger(log.LogIndex),
                Removed = log.Removed
            };
        }

        private string GenerateSubscriptionId()
        {
            var id = System.Threading.Interlocked.Increment(ref _idCounter);
            return "0x" + id.ToString("x");
        }
    }

    public class Subscription
    {
        public string Id { get; set; }
        public string ConnectionId { get; set; }
        public SubscriptionType Type { get; set; }
        public LogFilter LogFilter { get; set; }
    }

    public class SubscriptionNotification
    {
        public string SubscriptionId { get; set; }
        public string ConnectionId { get; set; }
        public object Payload { get; set; }
    }

    public enum SubscriptionType
    {
        NewHeads,
        Logs
    }
}
