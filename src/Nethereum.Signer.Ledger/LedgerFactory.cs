using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hid.Net;
using Ledger.Net;

namespace Nethereum.Ledger
{
    public class LedgerFactory
    {
        public class VendorProductIds
        {
            public VendorProductIds(int vendorId)
            {
                VendorId = vendorId;
            }
            public VendorProductIds(int vendorId, int? productId)
            {
                VendorId = vendorId;
                ProductId = productId;
            }
            public int VendorId
            {
                get;
            }
            public int? ProductId
            {
                get;
            }
        }

        public class UsageSpecification
        {
            public UsageSpecification(ushort usagePage, ushort usage)
            {
                UsagePage = usagePage;
                Usage = usage;
            }

            public ushort Usage
            {
                get;
            }
            public ushort UsagePage
            {
                get;
            }
        }
        public static VendorProductIds[] WellKnownLedgerWallets = new VendorProductIds[]
        {
            new VendorProductIds(0x2c97),
            new VendorProductIds(0x2581, 0x3b7c)
        };


        private static readonly UsageSpecification[] _UsageSpecification = new[] { new UsageSpecification(0xffa0, 0x01) };

        public static async Task<IHidDevice> GetWindowsConnectedLedgerHidDevice()
        {
            var devices = new List<DeviceInformation>();

            var collection = WindowsHidDevice.GetConnectedDeviceInformations();

            foreach (var ids in WellKnownLedgerWallets)
            {
                if (ids.ProductId == null)
                {
                    devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId));
                }
                else
                {
                    devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId && c.ProductId == ids.ProductId));
                }
            }

            var deviceFound = devices
                .FirstOrDefault(d =>
                    _UsageSpecification == null ||
                    _UsageSpecification.Length == 0 ||
                    _UsageSpecification.Any(u => d.UsagePage == u.UsagePage && d.Usage == u.Usage));

            var ledgerHidDevice = new WindowsHidDevice(deviceFound);
            await ledgerHidDevice.InitializeAsync();
            return ledgerHidDevice;
        }

        public static async Task<LedgerManager> GetWindowsConnectedLedgerManager()
        {
            var ledgerHidDevice = await GetWindowsConnectedLedgerHidDevice();
            return new LedgerManager(ledgerHidDevice);
        }
    }
}