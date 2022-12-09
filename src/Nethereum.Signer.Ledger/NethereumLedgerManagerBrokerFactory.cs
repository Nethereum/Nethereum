using Hid.Net.Windows;
using Ledger.Net;
using Usb.Net.Windows;

namespace Nethereum.Ledger
{
    public class NethereumLedgerManagerBrokerFactory
    {
        public static LedgerManagerBroker CreateWindowsHidUsb()
        {
           WindowsHidDeviceFactory.Register(null, null);
           WindowsUsbDeviceFactory.Register(null, null);

           return new LedgerManagerBroker(3000, new DefaultCoinUtility(), null, new NethereumLedgerManagerFactory());
           
        }

    }
}
