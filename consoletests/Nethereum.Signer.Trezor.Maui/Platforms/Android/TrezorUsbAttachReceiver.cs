#if ANDROID || __ANDROID__
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Hardware.Usb;
using Android.Util;
using Microsoft.Maui.ApplicationModel;
using Nethereum.Maui.AndroidUsb;

namespace Nethereum.Signer.Trezor.Maui.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false, Name = "com.nethereum.trezor.maui.TrezorUsbAttachReceiver")]
internal sealed class TrezorUsbAttachReceiver : BroadcastReceiver
{
    private const string Tag = "TrezorUsbReceiver";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null)
        {
            return;
        }

        var action = intent.Action;
        var device = (UsbDevice?)intent.GetParcelableExtra(UsbManager.ExtraDevice);
        if (device == null)
        {
            return;
        }

        Log.Info(Tag, $"USB event {action} for {device.DeviceName}");

        if (action == UsbManager.ActionUsbDeviceAttached)
        {
            var manager = (UsbManager?)context.GetSystemService(Context.UsbService);
            if (manager == null)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var granted = await UsbPermissionHelper.EnsurePermissionAsync(context, manager, device, CancellationToken.None);
                    Log.Info(Tag, $"Permission request result: {granted}");
                }
                catch (System.Exception ex)
                {
                    Log.Warn(Tag, $"Permission request failed: {ex.Message}");
                }
            });
        }
    }
}
#endif
