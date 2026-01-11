using Nethereum.DevChain.Server.Configuration;

namespace Nethereum.DevChain.Server.Forking
{
    public class ForkContext
    {
        public string Url { get; }
        public long BlockNumber { get; }
        public bool IsArchiveNode { get; set; }
        public bool ArchiveDetectionPerformed { get; set; }

        public ForkContext(ForkConfig config, long blockNumber)
        {
            Url = config.Url ?? throw new ArgumentNullException(nameof(config.Url));
            BlockNumber = blockNumber;
        }

        public static async Task<ForkContext?> CreateAsync(ForkConfig? config, Nethereum.Web3.Web3? web3)
        {
            if (config == null || string.IsNullOrEmpty(config.Url))
                return null;

            long blockNumber;
            if (config.BlockNumber.HasValue)
            {
                blockNumber = config.BlockNumber.Value;
            }
            else if (web3 != null)
            {
                var latestBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                blockNumber = (long)latestBlock.Value;
            }
            else
            {
                throw new InvalidOperationException("Fork URL specified but no web3 client available to fetch block number");
            }

            var context = new ForkContext(config, blockNumber);

            if (config.AutoDetectArchive && web3 != null)
            {
                await context.DetectArchiveNodeAsync(web3);
            }

            return context;
        }

        private async Task DetectArchiveNodeAsync(Nethereum.Web3.Web3 web3)
        {
            try
            {
                var blockParam = new Nethereum.RPC.Eth.DTOs.BlockParameter((ulong)BlockNumber);
                await web3.Eth.GetBalance.SendRequestAsync("0x0000000000000000000000000000000000000000", blockParam);
                IsArchiveNode = true;
            }
            catch
            {
                IsArchiveNode = false;
            }
            ArchiveDetectionPerformed = true;
        }
    }
}
