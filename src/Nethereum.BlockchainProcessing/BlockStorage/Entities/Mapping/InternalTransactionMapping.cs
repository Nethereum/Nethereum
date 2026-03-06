using System.Collections.Generic;

namespace Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping
{
    public static class InternalTransactionMapping
    {
        public static InternalTransaction CreateInternalTransaction(
            string txHash,
            int traceIndex,
            int depth,
            string type,
            string addressFrom,
            string addressTo,
            string value,
            string gas,
            string gasUsed,
            string input,
            string output,
            string error,
            string revertReason = null)
        {
            return new InternalTransaction
            {
                TransactionHash = txHash,
                TraceIndex = traceIndex,
                Depth = depth,
                Type = type,
                AddressFrom = addressFrom?.ToLowerInvariant(),
                AddressTo = addressTo?.ToLowerInvariant(),
                Value = value,
                Gas = gas,
                GasUsed = gasUsed,
                Input = input,
                Output = output,
                Error = error,
                RevertReason = revertReason,
                IsCanonical = true
            };
        }

        public static List<InternalTransaction> FlattenCallTrace(
            string txHash,
            string type,
            string from,
            string to,
            string value,
            string gas,
            string gasUsed,
            string input,
            string output,
            string error,
            List<CallTraceEntry> calls,
            int depth = 0,
            int startIndex = 0,
            string revertReason = null)
        {
            var result = new List<InternalTransaction>();

            var itx = CreateInternalTransaction(
                txHash, startIndex, depth, type, from, to, value, gas, gasUsed, input, output, error, revertReason);
            result.Add(itx);

            if (calls != null)
            {
                var nextIndex = startIndex + 1;
                foreach (var call in calls)
                {
                    var childResults = FlattenCallTrace(
                        txHash,
                        call.Type,
                        call.From,
                        call.To,
                        call.Value,
                        call.Gas,
                        call.GasUsed,
                        call.Input,
                        call.Output,
                        call.Error,
                        call.Calls,
                        depth + 1,
                        nextIndex,
                        call.RevertReason);
                    result.AddRange(childResults);
                    nextIndex += childResults.Count;
                }
            }

            return result;
        }
    }

    public class CallTraceEntry
    {
        public string Type { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Value { get; set; }
        public string Gas { get; set; }
        public string GasUsed { get; set; }
        public string Input { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public string RevertReason { get; set; }
        public List<CallTraceEntry> Calls { get; set; }
    }
}
