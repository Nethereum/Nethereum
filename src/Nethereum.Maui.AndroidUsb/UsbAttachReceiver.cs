#if ANDROID || __ANDROID__
using Android.Content;
using Android.Hardware.Usb;
using Android.Util;

namespace Nethereum.Maui.AndroidUsb;

[BroadcastReceiver(Enabled = true, Exported = false, Name = "com.nethereum.trezor.maui.UsbAttachReceiver")]
internal sealed class UsbAttachReceiver : BroadcastReceiver
{
    private const string Tag = "UsbAttachReceiver";

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

        Log.Info(Tag, $"USB event: {action} for device {device.DeviceName}");

        if (action == UsbManager.ActionUsbDeviceAttached)
        {
            // No-op: Maui app will enumerate via UsbManager once running.
        }
        else if (action == UsbManager.ActionUsbDeviceDetached)
        {
            // Optionally notify services about detaches.
        }
    }
}
#endif
