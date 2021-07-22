using System.Linq;
using System.Threading.Tasks;
using Hid.Net;
using Trezor.Net;

namespace Nethereum.Signer.Trezor
{
    public class TrezorFactory
    {
        public static async Task<IHidDevice> GetWindowsConnectedLedgerHidDeviceAsync()
        {
            var connectedDevices = WindowsHidDevice.GetConnectedDeviceInformations();

            var trezorDevices = connectedDevices.Where(d => d.VendorId == TrezorManager.TrezorVendorId && TrezorManager.TrezorProductId == 1).ToList();
            var trezorDeviceInformation = trezorDevices.FirstOrDefault(t => t.Product == TrezorManager.USBOneName);

            var trezorHidDevice = new WindowsHidDevice(trezorDeviceInformation);
            await trezorHidDevice.InitializeAsync().ConfigureAwait(false);
            return trezorHidDevice;
        }

        public static async Task<TrezorManager> GetWindowsConnectedLedgerManagerAsync(EnterPinArgs enterPinCallback)
        {
            var trezorHidDevice = await GetWindowsConnectedLedgerHidDeviceAsync().ConfigureAwait(false);
            return new TrezorManager(enterPinCallback, trezorHidDevice);
        }
    }
}
