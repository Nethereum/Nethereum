using System;
using System.Numerics;

namespace Nethereum.PrivacyPools.Relayer
{
    public enum RelayRequestStatus
    {
        Received,
        Validated,
        Broadcasted,
        Confirmed,
        Failed
    }

    public class RelayRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public BigInteger Scope { get; set; }
        public string Processooor { get; set; } = "";
        public byte[] WithdrawalData { get; set; } = new byte[0];
        public string ProofJson { get; set; } = "";
        public string PublicSignalsJson { get; set; } = "";
        public BigInteger RelayFeeBps { get; set; }
        public long Timestamp { get; set; }
    }

    public class RelayResult
    {
        public string RequestId { get; set; } = "";
        public RelayRequestStatus Status { get; set; }
        public string TransactionHash { get; set; }
        public string Error { get; set; }
        public long Timestamp { get; set; }

        public bool IsSuccess => Status == RelayRequestStatus.Broadcasted
                              || Status == RelayRequestStatus.Confirmed;

        public static RelayResult Success(string requestId, string txHash)
        {
            return new RelayResult
            {
                RequestId = requestId,
                Status = RelayRequestStatus.Broadcasted,
                TransactionHash = txHash,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public static RelayResult Failure(string requestId, string error)
        {
            return new RelayResult
            {
                RequestId = requestId,
                Status = RelayRequestStatus.Failed,
                Error = error,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
    }

    public class RelayRequestRecord
    {
        public string Id { get; set; } = "";
        public RelayRequestStatus Status { get; set; }
        public RelayRequest Request { get; set; }
        public string TransactionHash { get; set; }
        public string Error { get; set; }
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
    }
}
