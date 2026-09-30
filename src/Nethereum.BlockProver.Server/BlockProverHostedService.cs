using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Nethereum.BlockProver.Server
{
    public class BlockProverHostedService : BackgroundService
    {
        private readonly BlockProverProcessingService _service;

        public BlockProverHostedService(BlockProverProcessingService service)
        {
            _service = service;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => _service.ExecuteAsync(stoppingToken);
    }
}
