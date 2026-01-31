using Nethereum.EVM.Decoding;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Nethereum.EVM.StateChanges
{
    public class StateChangesResult
    {
        public List<BalanceChange> BalanceChanges { get; set; } = new List<BalanceChange>();
        public DecodedCall RootCall { get; set; }
        public List<DecodedLog> DecodedLogs { get; set; } = new List<DecodedLog>();
        public DecodedProgramResult DecodedResult { get; set; }
        public string Error { get; set; }
        public List<ProgramTrace> Traces { get; set; }
        public BigInteger GasUsed { get; set; }

        public bool HasError => !string.IsNullOrEmpty(Error);
        public bool HasBalanceChanges => BalanceChanges != null && BalanceChanges.Count > 0;
        public bool HasDecodedLogs => DecodedLogs != null && DecodedLogs.Count > 0;
        public bool HasTraces => Traces != null && Traces.Count > 0;

        public string ToSummaryString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== State Changes Preview ===");
            sb.AppendLine();

            if (HasError)
            {
                sb.AppendLine($"Error: {Error}");
                return sb.ToString();
            }

            if (HasBalanceChanges)
            {
                sb.AppendLine("Balance Changes:");
                foreach (var change in BalanceChanges)
                {
                    var sign = change.Change >= 0 ? "+" : "";
                    sb.AppendLine($"  {change.GetAddressDisplay()}: {sign}{change.Change} {change.GetDisplaySymbol()}");
                }
                sb.AppendLine();
            }

            if (RootCall != null)
            {
                sb.AppendLine("Call Tree:");
                AppendCallTree(sb, RootCall, 1);
                sb.AppendLine();
            }

            if (HasDecodedLogs)
            {
                sb.AppendLine($"Events ({DecodedLogs.Count}):");
                foreach (var log in DecodedLogs)
                {
                    sb.AppendLine($"  - {log.GetDisplayName()}");
                }
            }

            return sb.ToString();
        }

        private void AppendCallTree(StringBuilder sb, DecodedCall call, int indent)
        {
            var prefix = new string(' ', indent * 2);
            sb.AppendLine($"{prefix}{call.GetDisplayName()}");

            if (call.InnerCalls != null)
            {
                foreach (var innerCall in call.InnerCalls)
                {
                    AppendCallTree(sb, innerCall, indent + 1);
                }
            }
        }
    }
}
