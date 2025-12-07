#if ANDROID || __ANDROID__
using System;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;

namespace Nethereum.Maui.AndroidUsb;

public static class UsbPermissionHelper
{
    private const string PermissionAction = "net.nethereum.trezor.USB_PERMISSION";

    public static async Task<bool> EnsurePermissionAsync(
        Context context,
        UsbManager usbManager,
        UsbDevice device,
        CancellationToken cancellationToken)
    {
        if (usbManager.HasPermission(device))
        {
            return true;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        BroadcastReceiver? receiver = null;

        receiver = new UsbPermissionReceiver(device, granted =>
        {
            try
            {
                context.UnregisterReceiver(receiver);
            }
            catch
            {
                // ignored
            }

            tcs.TrySetResult(granted);
        });

        var intentFilter = new IntentFilter(PermissionAction);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            context.RegisterReceiver(receiver, intentFilter, ReceiverFlags.NotExported);
        }
        else
        {
            context.RegisterReceiver(receiver, intentFilter);
        }

        var intent = new Intent(PermissionAction).SetPackage(context.PackageName);
        var flags = PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetBroadcast(
            context,
            device.DeviceId,
            intent,
            flags);

        usbManager.RequestPermission(device, pendingIntent);

        using (cancellationToken.Register(() =>
               {
                   try
                   {
                       context.UnregisterReceiver(receiver);
                   }
                   catch
                   {
                       // ignored
                   }
                   tcs.TrySetCanceled(cancellationToken);
               }))
        {
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    private sealed class UsbPermissionReceiver : BroadcastReceiver
    {
        private readonly UsbDevice _device;
        private readonly Action<bool> _callback;

        public UsbPermissionReceiver(UsbDevice device, Action<bool> callback)
        {
            _device = device;
            _callback = callback;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent == null) return;
            var receivedDevice = (UsbDevice?)intent.GetParcelableExtra(UsbManager.ExtraDevice);
            if (receivedDevice == null || receivedDevice.DeviceId != _device.DeviceId) return;
            var granted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
            _callback(granted);
        }
    }
}
#endif
