using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Signer.Trezor.Abstractions;
using Nethereum.Signer.Trezor;
using Nethereum.Signer;

namespace Nethereum.Signer.Trezor.Maui.Services
{
    public class TrezorSigningService
    {
        private readonly ITrezorPromptHandler _promptHandler;
        private readonly ILoggerFactory _loggerFactory;
        private readonly NethereumTrezorManagerBrokerFactory.PlatformDeviceFactoryProviders _platformProviders;

        public TrezorSigningService(
            ITrezorPromptHandler promptHandler,
            ILoggerFactory loggerFactory,
            NethereumTrezorManagerBrokerFactory.PlatformDeviceFactoryProviders platformProviders)
        {
            _promptHandler = promptHandler;
            _loggerFactory = loggerFactory;
            _platformProviders = platformProviders;
        }

        public async Task<string> SignMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message cannot be empty.", nameof(message));

            var broker = NethereumTrezorManagerBrokerFactory.CreateDefault(_promptHandler, _loggerFactory, _platformProviders);
            var manager = await broker.WaitForFirstTrezorAsync().ConfigureAwait(false);
            var signer = new TrezorSessionExternalSigner(manager, 0);

            await signer.InitializeAsync().ConfigureAwait(false);
            var signature = await signer.SignAsync(Encoding.UTF8.GetBytes(message)).ConfigureAwait(false);

            return EthECDSASignature.CreateStringSignature(signature);
        }

        public async Task<bool> DetectDeviceAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var broker = NethereumTrezorManagerBrokerFactory.CreateDefault(_promptHandler, _loggerFactory, _platformProviders);
            try
            {
                var waitTask = broker.WaitForFirstTrezorAsync();
                var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
                var completed = await Task.WhenAny(waitTask, Task.Delay(effectiveTimeout, cancellationToken)).ConfigureAwait(false);

                if (completed == waitTask)
                {
                    var manager = await waitTask.ConfigureAwait(false);
                    manager?.Dispose();
                    return true;
                }

                return false;
            }
            finally
            {
                broker.Dispose();
            }
        }
    }
}
