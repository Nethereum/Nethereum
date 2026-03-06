using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Configuration;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public sealed class MessageIndexProcessingOptions
    {
        public string RpcUrl { get; set; } = "";
        public string HubContractAddress { get; set; } = "";
        public ulong TargetChainId { get; set; }
        public List<SourceChainOption> SourceChains { get; set; } = new();
        public BigInteger StartAtBlockNumber { get; set; } = 0;
        public uint MinimumBlockConfirmations { get; set; } = 12;
        public int ReorgBuffer { get; set; } = 0;
        public int BlocksPerRequest { get; set; } = 1000;
        public int RetryWeight { get; set; } = 50;

        public static MessageIndexProcessingOptions Load(IConfiguration configuration)
        {
            var options = new MessageIndexProcessingOptions
            {
                RpcUrl = configuration["RpcUrl"] ?? "",
                HubContractAddress = configuration["HubContractAddress"] ?? "",
            };

            if (ulong.TryParse(configuration["TargetChainId"], out var targetChainId))
                options.TargetChainId = targetChainId;

            if (BigInteger.TryParse(configuration["StartAtBlockNumber"], out var startBlock))
                options.StartAtBlockNumber = startBlock;

            if (uint.TryParse(configuration["MinimumBlockConfirmations"], out var confirmations))
                options.MinimumBlockConfirmations = confirmations;

            if (int.TryParse(configuration["ReorgBuffer"], out var reorgBuffer))
                options.ReorgBuffer = reorgBuffer;

            if (int.TryParse(configuration["BlocksPerRequest"], out var blocksPerReq))
                options.BlocksPerRequest = blocksPerReq;

            if (int.TryParse(configuration["RetryWeight"], out var retryWeight))
                options.RetryWeight = retryWeight;

            var sourceChains = configuration.GetSection("SourceChains").GetChildren();
            foreach (var child in sourceChains)
            {
                if (ulong.TryParse(child["ChainId"], out var chainId))
                {
                    options.SourceChains.Add(new SourceChainOption
                    {
                        ChainId = chainId,
                        RpcUrl = child["RpcUrl"] ?? "",
                        HubContractAddress = child["HubContractAddress"] ?? ""
                    });
                }
            }

            return options;
        }
    }

    public sealed class SourceChainOption
    {
        public ulong ChainId { get; set; }
        public string RpcUrl { get; set; } = "";
        public string HubContractAddress { get; set; } = "";
    }
}
