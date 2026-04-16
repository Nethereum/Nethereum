using System;
using System.Numerics;
using Microsoft.Extensions.Configuration;

namespace Nethereum.BlockchainStorage.Processors
{
    public sealed class BlockchainProcessingOptions
    {
        public string? BlockchainUrl { get; set; }
        public string? Name { get; set; }
        public ulong? MinimumBlockNumber { get; set; }
        public uint? MinimumBlockConfirmations { get; set; }
        public BigInteger? FromBlock { get; set; }
        public BigInteger? ToBlock { get; set; }
        public bool PostVm { get; set; } = false;
        public bool ProcessBlockTransactionsInParallel { get; set; } = true;
        public int NumberOfBlocksToProcessPerRequest { get; set; } = 1000;
        public int RetryWeight { get; set; } = 50;
        public int ReorgBuffer { get; set; } = 0;
        public bool UseBatchReceipts { get; set; } = true;

        /// <summary>
        /// When true, internal-transaction processing uses local EVM replay
        /// (<see cref="Nethereum.BlockchainProcessing.Services.EvmReplayInternalTransactionSource"/>)
        /// instead of <c>debug_traceTransaction</c>. Use on providers that gate
        /// the debug namespace.
        /// </summary>
        public bool UseLocalEvmReplayForInternalTransactions { get; set; } = false;

        /// <summary>
        /// Hardfork name for the EVM replay source (ignored when
        /// <see cref="UseLocalEvmReplayForInternalTransactions"/> is false).
        /// Must match the chain; defaults to "osaka".
        /// </summary>
        public string Hardfork { get; set; } = "osaka";

        public static BlockchainProcessingOptions Load(IConfiguration configuration)
        {
            var options = new BlockchainProcessingOptions();

            options.BlockchainUrl = configuration["BlockchainUrl"];
            options.Name = configuration["Blockchain"];
            options.MinimumBlockNumber = ParseUlong(configuration["MinimumBlockNumber"]);
            options.MinimumBlockConfirmations = ParseUint(configuration["MinimumBlockConfirmations"]);
            options.FromBlock = ParseBigInteger(configuration["FromBlock"]);
            options.ToBlock = ParseBigInteger(configuration["ToBlock"]);
            options.PostVm = ParseBool(configuration["PostVm"]) ?? options.PostVm;
            options.ProcessBlockTransactionsInParallel =
                ParseBool(configuration["ProcessBlockTransactionsInParallel"])
                ?? options.ProcessBlockTransactionsInParallel;
            options.NumberOfBlocksToProcessPerRequest =
                ParseInt(configuration["NumberOfBlocksToProcessPerRequest"])
                ?? options.NumberOfBlocksToProcessPerRequest;
            options.RetryWeight =
                ParseInt(configuration["RetryWeight"])
                ?? options.RetryWeight;
            options.ReorgBuffer =
                ParseInt(configuration["ReorgBuffer"])
                ?? options.ReorgBuffer;
            options.UseBatchReceipts =
                ParseBool(configuration["UseBatchReceipts"])
                ?? options.UseBatchReceipts;
            options.UseLocalEvmReplayForInternalTransactions =
                ParseBool(configuration["UseLocalEvmReplayForInternalTransactions"])
                ?? options.UseLocalEvmReplayForInternalTransactions;
            options.Hardfork = configuration["Hardfork"] ?? options.Hardfork;

            return options;
        }

        private static bool? ParseBool(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? (bool?)null : bool.Parse(value);
        }

        private static ulong? ParseUlong(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? (ulong?)null : ulong.Parse(value);
        }

        private static uint? ParseUint(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? (uint?)null : uint.Parse(value);
        }

        private static int? ParseInt(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? (int?)null : int.Parse(value);
        }

        private static BigInteger? ParseBigInteger(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? (BigInteger?)null : BigInteger.Parse(value);
        }
    }
}
