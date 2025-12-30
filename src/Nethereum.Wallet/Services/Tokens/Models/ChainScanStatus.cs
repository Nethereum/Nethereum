using System;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class ChainScanStatus
    {
        public ChainScanState State { get; set; } = ChainScanState.NotStarted;
        public string ErrorMessage { get; set; }
        public ChainScanErrorType? ErrorType { get; set; }
        public DateTime? LastAttempt { get; set; }
        public DateTime? LastSuccess { get; set; }
        public int ConsecutiveFailures { get; set; }
        public int HttpStatusCode { get; set; }

        public bool IsHealthy => State == ChainScanState.Ready && ConsecutiveFailures == 0;
        public bool HasError => State == ChainScanState.Error;
        public bool IsScanning => State == ChainScanState.Scanning;

        public void MarkScanning()
        {
            State = ChainScanState.Scanning;
            LastAttempt = DateTime.UtcNow;
        }

        public void MarkSuccess()
        {
            State = ChainScanState.Ready;
            LastSuccess = DateTime.UtcNow;
            ErrorMessage = null;
            ErrorType = null;
            ConsecutiveFailures = 0;
            HttpStatusCode = 0;
        }

        public void MarkError(string message, ChainScanErrorType errorType, int httpStatusCode = 0)
        {
            State = ChainScanState.Error;
            ErrorMessage = message;
            ErrorType = errorType;
            HttpStatusCode = httpStatusCode;
            ConsecutiveFailures++;
        }

        public static ChainScanStatus FromException(Exception ex)
        {
            var status = new ChainScanStatus
            {
                LastAttempt = DateTime.UtcNow
            };

            var message = ex.Message ?? "Unknown error";
            var innerMessage = ex.InnerException?.Message;

            if (message.Contains("402") || (innerMessage?.Contains("402") ?? false))
            {
                status.MarkError("Payment required - RPC quota exceeded", ChainScanErrorType.PaymentRequired, 402);
            }
            else if (message.Contains("429") || (innerMessage?.Contains("429") ?? false))
            {
                status.MarkError("Rate limited - too many requests", ChainScanErrorType.RateLimited, 429);
            }
            else if (message.Contains("401") || (innerMessage?.Contains("401") ?? false))
            {
                status.MarkError("Unauthorized - check API key", ChainScanErrorType.Unauthorized, 401);
            }
            else if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                     ex is TimeoutException ||
                     ex is OperationCanceledException)
            {
                status.MarkError("Request timed out", ChainScanErrorType.Timeout);
            }
            else if (message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("network", StringComparison.OrdinalIgnoreCase))
            {
                status.MarkError("Connection failed - check network", ChainScanErrorType.ConnectionFailed);
            }
            else
            {
                status.MarkError(message, ChainScanErrorType.Unknown);
            }

            return status;
        }
    }

    public enum ChainScanState
    {
        NotStarted,
        Scanning,
        Ready,
        Error
    }

    public enum ChainScanErrorType
    {
        Unknown,
        Timeout,
        ConnectionFailed,
        RateLimited,
        PaymentRequired,
        Unauthorized,
        InvalidResponse
    }
}
