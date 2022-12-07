using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Trezor.Net;
using Hid.Net.Windows;
using Usb.Net.Windows;
using Trezor.Net.Manager;
using Device.Net;
using static Nethereum.Signer.Trezor.Internal.ExtendedMessageType;
using Nethereum.Signer.Trezor.Internal;

namespace Nethereum.Signer.Trezor
{
    public class NethereumTrezorManagerBrokerFactory
    {
        public static ExtendedTrezorManagerBroker CreateWindowsHidUsb(EnterPinArgs enterPinCallback, EnterPinArgs enterPassPhrase, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
        {
            
            var hidFactory = TrezorManager.DeviceDefinitions.CreateWindowsHidDeviceFactory();
            var usbFactory = TrezorManager.DeviceDefinitions.CreateWindowsUsbDeviceFactory();
            var aggregateFactory = usbFactory.Aggregate(hidFactory, loggerFactory);
            return new ExtendedTrezorManagerBroker(enterPinCallback, enterPassPhrase, 2000, aggregateFactory, new DefaultCoinUtility(), loggerFactory);
        }
    }

    
}
