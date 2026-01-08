using Nethereum.ABI.FunctionEncoding;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Nethereum.EVM.Decoding
{
    public class DecodedProgramResult
    {
        public DecodedCall RootCall { get; set; }
        public List<DecodedLog> DecodedLogs { get; set; } = new List<DecodedLog>();
        public List<ParameterOutput> ReturnValue { get; set; } = new List<ParameterOutput>();
        public DecodedError RevertReason { get; set; }
        public bool IsRevert { get; set; }
        public bool IsSuccess => !IsRevert;
        public ProgramResult OriginalResult { get; set; }
        public CallInput OriginalCall { get; set; }
        public BigInteger ChainId { get; set; }

        public string ToHumanReadableString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== EVM Execution Result ===");
            sb.AppendLine();

            if (RootCall != null)
            {
                AppendCall(sb, RootCall, 0);
            }

            if (DecodedLogs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Events:");
                foreach (var log in DecodedLogs)
                {
                    sb.Append("  - ");
                    sb.Append(log.GetDisplayName());
                    if (log.IsDecoded && log.Parameters.Count > 0)
                    {
                        sb.Append("(");
                        sb.Append(FormatParameters(log.Parameters));
                        sb.Append(")");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
            if (IsRevert)
            {
                sb.Append("Result: REVERT");
                if (RevertReason != null)
                {
                    sb.Append(" - ");
                    sb.Append(RevertReason.GetDisplayMessage());
                }
                sb.AppendLine();
            }
            else
            {
                sb.Append("Result: SUCCESS");
                if (ReturnValue != null && ReturnValue.Count > 0)
                {
                    sb.Append(" => ");
                    sb.Append(FormatParameters(ReturnValue));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void AppendCall(StringBuilder sb, DecodedCall call, int indent)
        {
            var prefix = new string(' ', indent * 2);
            var arrow = indent > 0 ? "-> " : "";

            sb.Append(prefix);
            sb.Append(arrow);
            sb.Append(call.GetDisplayName());

            if (call.IsDecoded && call.InputParameters.Count > 0)
            {
                sb.Append("(");
                sb.Append(FormatParameters(call.InputParameters));
                sb.Append(")");
            }
            else if (!string.IsNullOrEmpty(call.RawInput) && call.RawInput.Length > 10)
            {
                sb.Append($"({call.RawInput.Substring(0, 10)}...)");
            }

            if (call.Value > 0)
            {
                sb.Append($" [value: {call.Value}]");
            }

            sb.AppendLine();

            foreach (var innerCall in call.InnerCalls)
            {
                AppendCall(sb, innerCall, indent + 1);
            }
        }

        private string FormatParameters(List<ParameterOutput> parameters)
        {
            if (parameters == null || parameters.Count == 0) return "";

            var parts = new List<string>();
            foreach (var param in parameters)
            {
                var name = param.Parameter?.Name ?? "";
                var value = FormatValue(param.Result);
                if (!string.IsNullOrEmpty(name))
                {
                    parts.Add($"{name}={value}");
                }
                else
                {
                    parts.Add(value);
                }
            }
            return string.Join(", ", parts);
        }

        private string FormatValue(object value)
        {
            if (value == null) return "null";

            if (value is byte[] bytes)
            {
                if (bytes.Length <= 32)
                {
                    return "0x" + Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(bytes);
                }
                return $"0x{Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(bytes).Substring(0, 20)}...({bytes.Length} bytes)";
            }

            if (value is string s)
            {
                if (s.Length > 50)
                {
                    return $"\"{s.Substring(0, 47)}...\"";
                }
                return $"\"{s}\"";
            }

            if (value is BigInteger bi)
            {
                return bi.ToString();
            }

            return value.ToString();
        }
    }
}
