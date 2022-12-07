using Device.Net;
using Microsoft.Extensions.Logging;
using Trezor.Net.Contracts;
using Trezor.Net;
using Trezor.Net.Manager;

namespace Nethereum.Signer.Trezor.Internal
{
    public class ExtendedTrezorManagerBroker : TrezorManagerBrokerBase<ExtendedTrezorManager, ExtendedMessageType.MessageType>
    {
        public ExtendedTrezorManagerBroker(EnterPinArgs enterPinArgs, EnterPinArgs enterPassphraseArgs, int? pollInterval, IDeviceFactory deviceFactory, ICoinUtility coinUtility = null, ILoggerFactory loggerFactory = null)
            : base(enterPinArgs, enterPassphraseArgs, pollInterval, deviceFactory, coinUtility, loggerFactory)
        {
        }

        protected override ExtendedTrezorManager CreateTrezorManager(IDevice device)
        {
            return new ExtendedTrezorManager(base.EnterPinArgs, base.EnterPassphraseArgs, device, base.LoggerFactory.CreateLogger<ExtendedTrezorManager>());
        }
    }
    }

