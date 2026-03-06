using System.Numerics;
using Microsoft.Extensions.Configuration;

namespace Nethereum.Mud.Repositories.Postgres
{
    public sealed class MudPostgresProcessingOptions
    {
        public string Address { get; set; } = string.Empty;
        public string RpcUrl { get; set; } = string.Empty;
        public BigInteger StartAtBlockNumberIfNotProcessed { get; set; } = 0;
        public int NumberOfBlocksToProcessPerRequest { get; set; } = 1000;
        public int RetryWeight { get; set; } = 50;
        public uint MinimumNumberOfConfirmations { get; set; } = 0;
        public int ReorgBuffer { get; set; } = 0;

        public static MudPostgresProcessingOptions Load(IConfiguration configuration)
        {
            var options = new MudPostgresProcessingOptions
            {
                Address = configuration["Address"] ?? string.Empty,
                RpcUrl = configuration["RpcUrl"] ?? string.Empty
            };

            options.StartAtBlockNumberIfNotProcessed =
                ParseBigInteger(configuration["StartAtBlockNumberIfNotProcessed"] ?? string.Empty)
                ?? options.StartAtBlockNumberIfNotProcessed;

            options.NumberOfBlocksToProcessPerRequest =
                ParseInt(configuration["NumberOfBlocksToProcessPerRequest"] ?? string.Empty)
                ?? options.NumberOfBlocksToProcessPerRequest;

            options.RetryWeight =
                ParseInt(configuration["RetryWeight"] ?? string.Empty)
                ?? options.RetryWeight;

            options.MinimumNumberOfConfirmations =
                ParseUint(configuration["MinimumNumberOfConfirmations"] ?? string.Empty)
                ?? options.MinimumNumberOfConfirmations;

            options.ReorgBuffer =
                ParseInt(configuration["ReorgBuffer"] ?? string.Empty)
                ?? options.ReorgBuffer;

            return options;
        }

        private static BigInteger? ParseBigInteger(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (BigInteger?)null : BigInteger.Parse(value);
        }

        private static int? ParseInt(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (int?)null : int.Parse(value);
        }

        private static uint? ParseUint(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (uint?)null : uint.Parse(value);
        }
    }
}
