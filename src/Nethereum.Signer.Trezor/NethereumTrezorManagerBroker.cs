using Device.Net;
using Microsoft.Extensions.Logging;
using Trezor.Net;
using Trezor.Net.Manager;
using Nethereum.Signer.Trezor.Internal;

namespace Nethereum.Signer.Trezor
{
    public class NethereumTrezorManagerBroker : TrezorManagerBrokerBase<ExtendedTrezorManager, ExtendedMessageType.MessageType>
    {
        public NethereumTrezorManagerBroker(EnterPinArgs enterPinArgs, EnterPinArgs enterPassphraseArgs, int? pollInterval, IDeviceFactory deviceFactory, ICoinUtility coinUtility = null, ILoggerFactory loggerFactory = null)
            : base(enterPinArgs, enterPassphraseArgs, pollInterval, deviceFactory, coinUtility, loggerFactory)
        {
        }

        protected override ExtendedTrezorManager CreateTrezorManager(IDevice device)
        {
            return new ExtendedTrezorManager(EnterPinArgs, EnterPassphraseArgs, device, LoggerFactory.CreateLogger<ExtendedTrezorManager>());
        }
    }
}

