
using Device.Net;
using Hardwarewallets.Net.Model;
using Hid.Net.Windows;
using Ledger.Net;
using Ledger.Net.Exceptions;
using Ledger.Net.Requests;
using Ledger.Net.Responses;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
using Usb.Net.Windows;
using System.Linq;

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
