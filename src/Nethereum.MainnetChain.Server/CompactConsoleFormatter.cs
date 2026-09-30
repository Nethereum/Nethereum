using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Nethereum.MainnetChain.Server
{
    internal sealed class CompactConsoleFormatter : ConsoleFormatter
    {
        public const string FormatterName = "nethereum-compact";

        private const string Reset = "[0m";

        private static readonly bool UseColor = !Console.IsOutputRedirected;

        public CompactConsoleFormatter() : base(FormatterName) { }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
            if (string.IsNullOrEmpty(message) && logEntry.Exception is null)
                return;

            textWriter.Write(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            textWriter.Write(' ');
            WriteLevel(textWriter, logEntry.LogLevel);
            textWriter.Write(' ');
            textWriter.Write(ShortCategory(logEntry.Category));
            textWriter.Write(": ");
            textWriter.Write(message);
            if (logEntry.Exception is not null)
            {
                textWriter.Write(' ');
                textWriter.Write(logEntry.Exception.ToString());
            }
            textWriter.Write(Environment.NewLine);
        }

        private static void WriteLevel(TextWriter writer, LogLevel level)
        {
            var (label, color) = level switch
            {
                LogLevel.Trace => ("trce", "[90m"),
                LogLevel.Debug => ("dbug", "[90m"),
                LogLevel.Information => ("info", "[32m"),
                LogLevel.Warning => ("warn", "[33m"),
                LogLevel.Error => ("fail", "[31m"),
                LogLevel.Critical => ("crit", "[1;31m"),
                _ => ("none", ""),
            };

            if (UseColor && color.Length > 0)
            {
                writer.Write(color);
                writer.Write(label);
                writer.Write(Reset);
            }
            else
            {
                writer.Write(label);
            }
        }

        private static string ShortCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
                return category;
            var lastDot = category.LastIndexOf('.');
            return lastDot >= 0 && lastDot < category.Length - 1 ? category.Substring(lastDot + 1) : category;
        }
    }
}
