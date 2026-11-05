using Nethereum.CoreChain.Models;

namespace Nethereum.CoreChain.Rpc
{
    internal static class LogFilterLimits
    {
        public const int MaxTopicPositions = 4;

        public static void Enforce(LogFilter filter, int logQueryLimit)
        {
            if (filter.Topics != null)
            {
                if (filter.Topics.Count > MaxTopicPositions)
                    throw RpcException.InvalidParams("exceed max topics");

                if (logQueryLimit > 0)
                {
                    foreach (var position in filter.Topics)
                    {
                        if (position != null && position.Count > logQueryLimit)
                            throw ExceedsLogQueryLimit();
                    }
                }
            }

            if (logQueryLimit > 0 && filter.Addresses != null && filter.Addresses.Count > logQueryLimit)
                throw ExceedsLogQueryLimit();
        }

        private static RpcException ExceedsLogQueryLimit() =>
            RpcException.InvalidParams("exceed max addresses or topics per search position");
    }
}
