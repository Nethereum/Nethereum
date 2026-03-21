using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.PrivacyPools.Relayer
{
    public interface IRelayRequestStore
    {
        Task<RelayRequestRecord> CreateAsync(RelayRequest request);
        Task UpdateStatusAsync(string requestId, RelayRequestStatus status, string transactionHash = null, string error = null);
        Task<RelayRequestRecord> GetAsync(string requestId);
        Task<List<RelayRequestRecord>> GetByStatusAsync(RelayRequestStatus status);
    }

    public class InMemoryRelayRequestStore : IRelayRequestStore
    {
        private readonly Dictionary<string, RelayRequestRecord> _records = new Dictionary<string, RelayRequestRecord>();
        private readonly object _lock = new object();

        public Task<RelayRequestRecord> CreateAsync(RelayRequest request)
        {
            var record = new RelayRequestRecord
            {
                Id = request.Id,
                Status = RelayRequestStatus.Received,
                Request = request,
                CreatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UpdatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            lock (_lock) { _records[record.Id] = record; }
            return Task.FromResult(record);
        }

        public Task UpdateStatusAsync(string requestId, RelayRequestStatus status, string transactionHash = null, string error = null)
        {
            lock (_lock)
            {
                if (_records.TryGetValue(requestId, out var record))
                {
                    record.Status = status;
                    if (transactionHash != null) record.TransactionHash = transactionHash;
                    if (error != null) record.Error = error;
                    record.UpdatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
            }
            return Task.FromResult(0);
        }

        public Task<RelayRequestRecord> GetAsync(string requestId)
        {
            lock (_lock)
            {
                _records.TryGetValue(requestId, out var record);
                return Task.FromResult(record);
            }
        }

        public Task<List<RelayRequestRecord>> GetByStatusAsync(RelayRequestStatus status)
        {
            lock (_lock)
            {
                var result = new List<RelayRequestRecord>();
                foreach (var record in _records.Values)
                {
                    if (record.Status == status)
                        result.Add(record);
                }
                return Task.FromResult(result);
            }
        }
    }
}
